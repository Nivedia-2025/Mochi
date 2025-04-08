using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Mochi
{
    public class OptimizER : GH_Component
    {
        public OptimizER()
          : base("OptimizER", "Optimize",
              "Optimizes rotation of Breps to minimize bounding box area.",
              "Mochi", "Geometry")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("Surfaces", "S", "Input Breps to optimize", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Rotation Angle", "A", "Manual rotation angle in degrees (only used if Optimize is false)", GH_ParamAccess.item, 0);
            pManager.AddBooleanParameter("Optimize", "O", "Enable rotation optimization", GH_ParamAccess.item, true);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("Rotated Surfaces", "R", "Rotated Breps", GH_ParamAccess.list);
            pManager.AddRectangleParameter("Bounding Boxes", "B", "Bounding boxes of rotated Breps", GH_ParamAccess.list);
            pManager.AddNumberParameter("Areas", "A", "Areas of the bounding boxes", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Brep> surfaces = new List<Brep>();
            int rotationAngle = 0;
            bool optimize = true;

            if (!DA.GetDataList(0, surfaces)) return;
            if (!DA.GetData(1, ref rotationAngle)) return;
            if (!DA.GetData(2, ref optimize)) return;

            List<Brep> rotatedSurfaces = new List<Brep>();
            List<Rectangle3d> boundingBoxes = new List<Rectangle3d>();
            List<double> areas = new List<double>();

            foreach (Brep surface in surfaces)
            {
                if (!optimize)
                {
                    Brep rotated = RotateSurface(surface, rotationAngle);
                    Rectangle3d bbox = ComputeBoundingBox(rotated);

                    rotatedSurfaces.Add(rotated);
                    boundingBoxes.Add(bbox);
                    areas.Add(bbox.Area);
                    continue;
                }

                // Optimization mode
                List<double> allAreas = new List<double>();
                List<Brep> rotations = new List<Brep>();
                List<Rectangle3d> boxes = new List<Rectangle3d>();

                for (int i = 0; i < 360; i += 5)
                {
                    Brep rotated = RotateSurface(surface, i);
                    Rectangle3d bbox = ComputeBoundingBox(rotated);

                    rotations.Add(rotated);
                    boxes.Add(bbox);
                    allAreas.Add(bbox.Area);
                }

                int minIndex = allAreas.IndexOf(allAreas.Min());
                rotatedSurfaces.Add(rotations[minIndex]);
                boundingBoxes.Add(boxes[minIndex]);
                areas.Add(allAreas[minIndex]);
            }

            DA.SetDataList(0, rotatedSurfaces);
            DA.SetDataList(1, boundingBoxes);
            DA.SetDataList(2, areas);
        }

        private Brep RotateSurface(Brep brep, double angle)
        {
            Brep copy = brep.DuplicateBrep();
            Point3d center = brep.GetBoundingBox(true).Center;
            Transform rotation = Transform.Rotation(Rhino.RhinoMath.ToRadians(angle), Vector3d.ZAxis, center);
            copy.Transform(rotation);
            return copy;
        }

        private Rectangle3d ComputeBoundingBox(Brep brep)
        {
            Curve outerCurve = brep.Faces[0].OuterLoop.To3dCurve();
            BoundingBox bbox = outerCurve.GetBoundingBox(true);
            return new Rectangle3d(Plane.WorldXY, new Interval(bbox.Min.X, bbox.Max.X), new Interval(bbox.Min.Y, bbox.Max.Y));
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override System.Drawing.Bitmap Icon => null; // Add your icon if needed
        public override Guid ComponentGuid => new Guid("a29fe4e1-b86b-4e2f-859d-2b234abb935c");
    }
}