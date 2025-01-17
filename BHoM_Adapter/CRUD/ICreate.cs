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
using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using BH.oM.Data.Requests;
using BH.oM.Adapter;

namespace BH.Adapter
{
    // NOTE: CRUD folder methods
    // All methods in the CRUD folder are used as "back-end" methods by the Adapter itself.
    // They are meant to be implemented at the Toolkit level.
    public abstract partial class BHoMAdapter
    {
        /***************************************************/
        /**** Basic methods                             ****/
        /***************************************************/
        /* These methods provide the basic functionalities for the CRUD to work. */

        // The Create must only contain the logic that generates the objects in the external software.
        // It is primarily called by the Push, in the context of the CRUD method, and also by other methods that require it (Update, UpdateProperty).
        // It must be implemented (overrided) at the Toolkit level.
        protected virtual bool ICreate<T>(IEnumerable<T> objects, ActionConfig actionConfig = null) where T : IBHoMObject
        {
            // To be overridden in the specific adapter. 
            // It must only include a dynamic dispatch to your type-specific Create implementations, in other words:
            // Create(objects as dynamic);
            return false;  
        }

        // Write your type-specific implementations of Create in your Toolkit, like
        // protected bool Create(IEnumerable<Node> node) 
        // { 
        //      //code to do the Create of nodes, including any COM-call. 
        // }
    }
}


