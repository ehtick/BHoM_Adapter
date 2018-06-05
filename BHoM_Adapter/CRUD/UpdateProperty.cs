﻿using System.Collections.Generic;
using System.Linq;
using BH.oM.Base;
using BH.oM.DataManipulation.Queries;
using BH.Engine.Reflection;
using BH.Engine.DataManipulation;

namespace BH.Adapter
{
    public abstract partial class BHoMAdapter
    {
        /***************************************************/
        /**** Protected Methods                         ****/
        /***************************************************/

        public int PullUpdatePush(FilterQuery filter, string property, object newValue) 
        {
            if (Config.ProcessInMemory)
            {
                IEnumerable<IBHoMObject> objects = UpdateInMemory(filter, property, newValue);
                Create(objects, true);
                return objects.Count();
            }
            else
                return UpdateThroughAPI(filter, property, newValue);
        }


        /***************************************************/
        /**** Helper Methods                            ****/
        /***************************************************/

        public IEnumerable<IBHoMObject> UpdateInMemory(FilterQuery filter, string property, object newValue)
        {
            // Pull the objects to update
            IEnumerable<IBHoMObject> objects = Read(filter.Type);

            // Set the property of the objects matching the filter
            filter.FilterData(objects).ToList().SetPropertyValue(filter.Type, property, newValue);

            return objects;
        }

        /***************************************************/

        public int UpdateThroughAPI(FilterQuery filter, string property, object newValue)
        {
            IEnumerable<object> ids = Pull(filter).Select(x => ((IBHoMObject)x).CustomData[AdapterId]);
            return UpdateProperty(filter.Type, ids, property, newValue);
        }
    }
}