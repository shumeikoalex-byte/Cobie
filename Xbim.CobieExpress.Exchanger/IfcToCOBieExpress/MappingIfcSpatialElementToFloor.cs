﻿using System.Collections.Generic;
using System.Linq;
using Xbim.Ifc4.Interfaces;

namespace Xbim.CobieExpress.Exchanger
{
    internal class MappingIfcSpatialElementToFloor : MappingIfcObjectToAsset<IIfcSpatialElement, CobieFloor>
    {
        private MappingStringToCategory _stringToCategory;
        private MappingIfcSpatialElementToSpace _spatialStructureToSpace;

        public MappingStringToCategory StringToCategory
        {
            get {
                return _stringToCategory ??
                       (_stringToCategory = Exchanger.GetOrCreateMappings<MappingStringToCategory>());
            }
        }

        public MappingIfcSpatialElementToSpace SpatialStructureToSpace
        {
            get { return _spatialStructureToSpace ?? (_spatialStructureToSpace = Exchanger.GetOrCreateMappings<MappingIfcSpatialElementToSpace>()); }
        }


        protected override CobieFloor Mapping(IIfcSpatialElement ifcSpatialStructureElement, CobieFloor target)
        {
            base.Mapping(ifcSpatialStructureElement, target);

            if (!string.IsNullOrWhiteSpace(ifcSpatialStructureElement.LongName))
                target.Description = ifcSpatialStructureElement.LongName;

            IEnumerable<IIfcSpatialElement> spaces = null;
            var site = ifcSpatialStructureElement as IIfcSite;
            var building = ifcSpatialStructureElement as IIfcBuilding;
            var storey = ifcSpatialStructureElement as IIfcBuildingStorey;
            var spaceElement = ifcSpatialStructureElement as IIfcSpace;
            if (site != null)
            {
                if(target.Categories.Count ==0)
                    target.Categories.Add(StringToCategory.GetOrCreate("Site"));
                //upgrade code below to use extension method GetSpaces()

                if (site.IsDecomposedBy != null)
                {
                    var decomp = site.IsDecomposedBy;
                    var objs = decomp.SelectMany(s => s.RelatedObjects);
                    spaces = objs.OfType<IIfcSpace>();
                }

            }
            else if (building != null)
            {
                if (target.Categories.Count == 0)
                    target.Categories.Add(StringToCategory.GetOrCreate("Building"));
                spaces = building.Spaces;
            }
            else if (storey != null)
            {
                if (target.Categories.Count == 0)
                    target.Categories.Add(StringToCategory.GetOrCreate("Floor"));
                if (storey.Elevation.HasValue)
                {
                    target.Elevation = storey.Elevation.Value;
                }
                spaces = storey.Spaces;
            }
            else if (spaceElement != null)
            {
                if (target.Categories.Count == 0)
                    target.Categories.Add(StringToCategory.GetOrCreate("Space"));
                spaces = spaceElement.Spaces;
            }

            Helper.TrySetSimpleValue<double>("FloorHeightValue", ifcSpatialStructureElement, f => target.Height = f);

            //Add spaces
            var ifcSpatialStructureElements = spaces != null ? spaces.ToList() : new List<IIfcSpatialElement>();

            // Previously used to create 'PlaceholderSpaces' but has been removed from CObieExpress.
            // https://github.com/xBimTeam/XbimExchange/commit/1ef25829a1a8d39a76f70010bedf06f1418954ad 

            //ifcSpatialStructureElements.Add(ifcSpatialStructureElement);

            foreach (var element in ifcSpatialStructureElements)
            {
                CobieSpace space;
                if (!SpatialStructureToSpace.GetOrCreateTargetObject(element.EntityLabel, out space)) continue;

                space = SpatialStructureToSpace.AddMapping(element, space);
                space.Floor = target;
            }

            if(target.Height == null)
            {
                target.Height = target.Spaces.Max(s=> s.UsableHeight);
            }

            //TODO: Floor Issues

            return target;
        }

        public override CobieFloor CreateTargetObject()
        {
            return Exchanger.TargetRepository.Instances.New<CobieFloor>();
        }
    }
}
