/*
 * This file is part of the Buildings and Habitats object Model (BHoM)
 * Copyright (c) 2015 - 2022, the respective contributors. All rights reserved.
 *
 * Each contributor holds copyright over their respective contributions.
 * The project versioning (Git) records all such contribution source information.
 *                                           
 *                                                                              
 * The BHoM is free software: you can redistribute it and/or modify         
 * it under the terms of the GNU Lesser General Public License as published by  
 * the Free Software Foundation, either version 3.0 of the License, or          
 * (at your option) any later version.                                          
 *                                                                              
 * The BHoM is distributed in the hope that it will be useful,              
 * but WITHOUT ANY WARRANTY; without even the implied warranty of               
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the                 
 * GNU Lesser General Public License for more details.                          
 *                                                                            
 * You should have received a copy of the GNU Lesser General Public License     
 * along with this code. If not, see <https://www.gnu.org/licenses/lgpl-3.0.html>.      
 */

using BH.oM.Base;
using BH.oM.Data.Collections;
using BH.Engine.Adapter;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using BH.oM.Diffing;
using BH.oM.Adapter;
using BH.Engine.Base;

namespace BH.Adapter
{
    public abstract partial class BHoMAdapter
    {
        /***************************************************/
        /**** Push CRUD Dispatchers                     ****/
        /***************************************************/
        // These methods dispatch calls to different CRUD methods as required by the Push.

        [Description("Performs the full CRUD, calling the single CRUD methods as appropriate.")]
        protected bool FullCRUD<T>(IEnumerable<T> objectsToPush, PushType pushType = PushType.AdapterDefault, string tag = "", ActionConfig actionConfig = null) where T : class, IBHoMObject
        {
            // Make sure objects are distinct 
            List<T> newObjects = objectsToPush.Distinct(Engine.Adapter.Query.GetComparerForType<T>(this)).ToList();

            // Add the tag if provided
            if (!string.IsNullOrWhiteSpace(tag))
                newObjects.ForEach(x => x.Tags.Add(tag));

            //Read all the objects of that type from the external model
            IEnumerable<T> readObjects;
            if (tag != "" || Engine.Adapter.Query.GetComparerForType<T>(this) != EqualityComparer<T>.Default)
                readObjects = Read(typeof(T), "", actionConfig)?.Where(x => x != null && x is T).Cast<T>();
            else
                readObjects = new List<T>();

            readObjects = readObjects ?? new List<T>(); // null guard for readObjects

            // Merge and push the dependencies
            if (m_AdapterSettings.HandleDependencies)
            {
                var dependencyTypes = Engine.Adapter.Query.GetDependencyTypes<T>(this);
                var dependencyObjects = Engine.Adapter.Query.GetDependencyObjects(objectsToPush, dependencyTypes, this);

                foreach (var depObj in dependencyObjects)
                    if (!FullCRUD(depObj.Value as dynamic, pushType, tag, actionConfig))
                        return false;
            }

            // Replace objects that overlap and define the objects that still have to be pushed
            IEnumerable<T> objectsToCreate;

            if (pushType == PushType.DeleteThenCreate)
            {
                // All objects read from the model are to be deleted. 
                // Note that this means that only objects of the same type of the objects being pushed will be deleted.
                if (readObjects.Any())
                    IDelete(typeof(T), readObjects.Select(obj => obj.AdapterIds(AdapterIdFragmentType)), actionConfig);

                objectsToCreate = newObjects;
            }
            else if (m_AdapterSettings.ProcessInMemory)
                objectsToCreate = ReplaceInMemory(newObjects, readObjects, tag);
            else
                objectsToCreate = ReplaceThroughAPI(newObjects, readObjects, tag, actionConfig, pushType);

            // Assign Id if needed
            if (m_AdapterSettings.UseAdapterId)
                AssignNextFreeId(objectsToCreate);

            // Create objects
            if (!ICreate(objectsToCreate, actionConfig))
                return false;

            if (m_AdapterSettings.UseAdapterId)
            {
                // Map Ids to the original set of objects (before we extracted the distincts elements from it).
                // If some objects of the original set were not Created (because e.g. they were already existing in the external model and had already an id, 
                // therefore no new id was assigned to them) they will not get mapped, so the original set will be left with them intact.
                IEqualityComparer<T> comparer = Engine.Adapter.Query.GetComparerForType<T>(this);
                foreach (T item in objectsToPush)
                {
                    // Fetch any existing IAdapterId fragment and assign it to the item.
                    // This preserves any additional property other than `Id` that may be in the fragment.
                    IFragment fragment;
                    newObjects.First(x => comparer.Equals(x, item)).Fragments.TryGetValue(AdapterIdFragmentType, out fragment);

                    item.SetAdapterId(fragment as IAdapterId);
                }
            }

            return true;
        }

        /***************************************************/

        protected IEnumerable<T> ReplaceInMemory<T>(IEnumerable<T> newObjects, IEnumerable<T> existingOjects, string tag, bool mergeWithComparer = false) where T : class, IBHoMObject
        {
            // Separate objects based on tags
            List<T> multiTaggedObjects = existingOjects.Where(x => x.Tags.Contains(tag) && x.Tags.Count > 1).ToList();
            IEnumerable<T> nonTaggedObjects = existingOjects.Where(x => !x.Tags.Contains(tag));

            // Remove the tag from the multi-tags objects
            multiTaggedObjects.ForEach(x => x.Tags.Remove(tag));

            // Merge objects if required
            if (mergeWithComparer)
            {
                VennDiagram<T> diagram = Engine.Data.Create.VennDiagram(
                        newObjects, multiTaggedObjects.Concat(nonTaggedObjects),
                        Engine.Adapter.Query.GetComparerForType<T>(this));

                List<ICopyPropertiesModule<T>> copyPropertiesModules = this.GetCopyPropertiesModules<T>();

                diagram.Intersection.ForEach(x =>
                {
                    CopyBHoMObjectProperties(x.Item1, x.Item2);
                    copyPropertiesModules.ForEach(m => m.CopyProperties(x.Item1, x.Item2));
                });

                newObjects = diagram.OnlySet1;
            }

            // Generate the list of objects to push
            IEnumerable<T> objectsToCreate = multiTaggedObjects.Concat(nonTaggedObjects).Concat(newObjects);
            return objectsToCreate;
        }

        /***************************************************/

        protected IEnumerable<T> ReplaceThroughAPI<T>(IEnumerable<T> objsToPush, IEnumerable<T> readObjs, string tag, ActionConfig actionConfig, PushType pushType) where T : class, IBHoMObject
        {
            IEqualityComparer<T> comparer = Engine.Adapter.Query.GetComparerForType<T>(this);
            VennDiagram<T> diagram = Engine.Data.Create.VennDiagram(objsToPush, readObjs, comparer);

            // Objects to push that do not have any overlap with the read ones
            List<T> objsToPush_exclusive = diagram.OnlySet1.ToList();

            // Objects existing in the model that do not have any overlap with the objects being pushed
            List<T> readObjs_exclusive = diagram.OnlySet2.ToList();

            // Do not consider exclusive read objects that do not contain the currently specified tag. 
            // Those objects do not need any update, so they will be left as they are.
            readObjs_exclusive.RemoveAll(x => !x.Tags.Contains(tag));

            // Remove the current tag from exclusive read objects
            readObjs_exclusive.ForEach(x => x.Tags.Remove(tag));

            // Objects that do not have any other tag, except the current tag, are to be Deleted from the model.
            IEnumerable<T> toBeDeleted = readObjs_exclusive.Where(x => x.Tags.Count == 0);

            // Extract the adapterIds from the toBeDeleted and call Delete() for all of them.
            if (pushType != PushType.UpdateOrCreateOnly && toBeDeleted != null && toBeDeleted.Any())
                IDelete(typeof(T), toBeDeleted.Select(obj => obj.AdapterIds(AdapterIdFragmentType)), actionConfig);

            // Update the tags for the rest of the existing objects in the model
            IUpdateTags(typeof(T),
                readObjs_exclusive.Where(x => x.Tags.Count > 0).Select(x => x.AdapterIds(AdapterIdFragmentType)),
                readObjs_exclusive.Where(x => x.Tags.Count > 0).Select(x => x.Tags),
                actionConfig);

            // For the objects that have an overlap between existing and pushed 
            // (e.g. an end Node of a Bar being pushed is overlapping with the End Node of a Bar already in the model)
            // there might be properties that need to be preserved (e.g. node constraints).
            // Port (copy over) those properties from the readObjs to the objToPush.

            List<ICopyPropertiesModule<T>> copyPropertiesModules = this.GetCopyPropertiesModules<T>();

            diagram.Intersection.ForEach(x =>
                {
                    CopyBHoMObjectProperties(x.Item1, x.Item2);
                    copyPropertiesModules.ForEach(m => m.CopyProperties(x.Item1 as dynamic, x.Item2 as dynamic));
                });


            if (pushType == PushType.FullPush || pushType == PushType.UpdateOrCreateOnly)
            {
                // Update the overlapping objects (between read and toPush), with the now ported properties.
                if (diagram.Intersection != null && diagram.Intersection.Any())
                    IUpdate(diagram.Intersection.Select(x => x.Item1), actionConfig);
            }
            else if(pushType == PushType.CreateNonExisting)
            {
                //For CreateNonExisting, the overlap objects are just kept, and not updated. To make sure tag functionality works though, 
                //The obejcts need to get their tags (if any) updated.
                IUpdateTags(typeof(T),
                    diagram.Intersection.Where(x => x.Item1.Tags.Count > 0).Select(x => x.Item1.AdapterIds(AdapterIdFragmentType)),
                    diagram.Intersection.Where(x => x.Item1.Tags.Count > 0).Select(x => x.Item1.Tags),
                    actionConfig);
            }


            // Return the objectsToPush that do not have any overlap with the existing ones; those will need to be created
            return objsToPush_exclusive;
        }


    }
}



