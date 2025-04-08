using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Rhino;
using Rhino.Geometry;

namespace Mochi
{
    public class UnrollER : GH_Component
    {
        public UnrollER()
          : base("UnrollER", "Unroll",
              "Unrolls and arranges planar segments into planar strips flat along XY plane.",
              "Mochi", "Geometry")
        { }

        public override Guid ComponentGuid => new Guid("d3f25c66-9c0c-4c7e-91f6-df2c7c08c369");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("Strips", "S", "List of planar Brep strips to unroll", GH_ParamAccess.list);
            pManager.AddNumberParameter("Spacing", "Sp", "Spacing between unrolled strips", GH_ParamAccess.item, 10.0);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("Unrolled Strips", "U", "Strips unrolled and positioned on the XY plane", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Brep> strips = new List<Brep>();
            double spacing = 10.0;

            if (!DA.GetDataList(0, strips)) return;
            if (!DA.GetData(1, ref spacing)) return;

            List<Brep> positionedStrips = new List<Brep>();

            if (strips == null || strips.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No strips provided.");
                return;
            }

            // Find the "lowest" strip (closest to world origin in Z)
            Brep targetStrip = null;
            double minHeight = double.MaxValue;

            foreach (var strip in strips)
            {
                double minZ = double.MaxValue;
                foreach (var edge in strip.Edges)
                {
                    minZ = Math.Min(minZ, edge.PointAtStart.Z);
                    minZ = Math.Min(minZ, edge.PointAtEnd.Z);
                }

                if (minZ < minHeight)
                {
                    targetStrip = strip;
                    minHeight = minZ;
                }
            }

            if (targetStrip == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Could not determine base strip for alignment.");
                return;
            }

            double xOffset = 0;
            double yOffset = 0;

            foreach (var strip in strips)
            {
                Brep orientedStrip = strip.DuplicateBrep();

                // Get largest face
                BrepFace largestFace = null;
                double maxArea = 0;

                foreach (BrepFace face in orientedStrip.Faces)
                {
                    var areaProps = AreaMassProperties.Compute(face);
                    if (areaProps == null) continue;

                    double area = areaProps.Area;
                    if (area > maxArea)
                    {
                        maxArea = area;
                        largestFace = face;
                    }
                }

                if (largestFace == null) continue;

                // Align to XY
                if (!largestFace.TryGetPlane(out Plane facePlane)) continue;

                Transform toXY = Transform.PlaneToPlane(facePlane, Plane.WorldXY);
                orientedStrip.Transform(toXY);

                // For base strip: align to Z = 0
                if (strip == targetStrip)
                {
                    BoundingBox bbox = orientedStrip.GetBoundingBox(true);
                    double zOffset = -bbox.Min.Z;
                    orientedStrip.Transform(Transform.Translation(0, 0, zOffset));
                }

                // Place in grid
                orientedStrip.Transform(Transform.Translation(xOffset, yOffset, 0));
                positionedStrips.Add(orientedStrip);

                double stripWidth = orientedStrip.GetBoundingBox(true).Diagonal.Length;
                xOffset += stripWidth + spacing;

                // Optional: wrap to next row if exceeds width
                if (xOffset > 500) // Adjust grid size if needed
                {
                    xOffset = 0;
                    yOffset += stripWidth + spacing;
                }
            }

            DA.SetDataList(0, positionedStrips);
        }
    }
}
