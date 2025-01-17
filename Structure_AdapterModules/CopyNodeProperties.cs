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
using BH.oM.Adapter;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.ComponentModel;
using BH.oM.Structure.Elements;
using BH.oM.Structure.SectionProperties;

namespace BH.Adapter.Modules
{
    public class CopyNodeProperties : ICopyPropertiesModule<Node>
    {
        [Description("Gets called during the Push, when you have overlapping nodes." +
            "Takes properties specified from the source Node and assigns them to the target Node.")]
        public void CopyProperties(Node target, Node source)
        {
            // If source is constrained and target is not, add source constraint to target
            if (source.Support != null && target.Support == null)
                target.Support = source.Support;

            // If source has a defined orientation and target does not, add local orientation from the source
            if (source.Orientation != null && target.Orientation == null)
                target.Orientation = source.Orientation;
        }
    }
}



