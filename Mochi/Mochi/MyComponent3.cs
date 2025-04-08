using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Mochi
{
    public class PackER : GH_Component
    {
        public PackER()
          : base("PackER", "Pack",
              "Packs the segmented planar geometries efficiently",
              "Mochi", "Geometry")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddRectangleParameter("Bounding Boxes", "B", "Bounding boxes of the surfaces", GH_ParamAccess.list);
            pManager.AddBrepParameter("Rotated Surfaces", "S", "Corresponding rotated Breps", GH_ParamAccess.list);
            pManager.AddNumberParameter("Sheet Width", "W", "Width of the sheet", GH_ParamAccess.item);
            pManager.AddNumberParameter("Sheet Height", "H", "Height of the sheet", GH_ParamAccess.item);
            pManager.AddNumberParameter("Spacing", "Sp", "Spacing between geometries", GH_ParamAccess.item, 5.0);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddRectangleParameter("Packed Boxes", "PB", "Packed bounding boxes on sheets", GH_ParamAccess.list);
            pManager.AddRectangleParameter("Sheet Outlines", "SO", "Outlines of each sheet", GH_ParamAccess.list);
            pManager.AddBrepParameter("Placed Surfaces", "PS", "Transformed Breps placed within sheets", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Rectangle3d> boundingBoxes = new List<Rectangle3d>();
            List<Brep> rotatedSurfaces = new List<Brep>();
            double sheetWidth = 0.0, sheetHeight = 0.0, spacing = 5.0;

            if (!DA.GetDataList(0, boundingBoxes)) return;
            if (!DA.GetDataList(1, rotatedSurfaces)) return;
            if (!DA.GetData(2, ref sheetWidth)) return;
            if (!DA.GetData(3, ref sheetHeight)) return;
            DA.GetData(4, ref spacing);

            Dictionary<Rectangle3d, Brep> bboxToSurface = new Dictionary<Rectangle3d, Brep>();
            List<Rectangle3d> uniqueBoundingBoxes = new List<Rectangle3d>();
            List<Brep> placedSurfaces = new List<Brep>();

            if (rotatedSurfaces != null && rotatedSurfaces.Count > 0)
            {
                foreach (var surface in rotatedSurfaces)
                {
                    BoundingBox surfaceBbox = surface.GetBoundingBox(true);
                    Rectangle3d rect = new Rectangle3d(Plane.WorldXY, surfaceBbox.Min, surfaceBbox.Max);

                    if (!bboxToSurface.ContainsKey(rect))
                    {
                        bboxToSurface[rect] = surface;
                        uniqueBoundingBoxes.Add(rect);
                    }
                }
            }

            foreach (var bbox in boundingBoxes)
            {
                if (!bboxToSurface.ContainsKey(bbox))
                {
                    uniqueBoundingBoxes.Add(bbox);
                }
            }

            uniqueBoundingBoxes = uniqueBoundingBoxes.OrderByDescending(b => b.Width * b.Height).ToList();

            List<List<Rectangle3d>> sheets = new List<List<Rectangle3d>>();
            List<Rectangle3d> currentSheet = new List<Rectangle3d>();
            List<Rectangle3d> sheetOutlines = new List<Rectangle3d>();

            double sheetStartX = 0;
            double xOffset = 0, yOffset = 0;
            double maxRowHeight = 0;

            foreach (var bbox in uniqueBoundingBoxes)
            {
                double width = bbox.Width;
                double height = bbox.Height;

                if (xOffset + width > sheetWidth)
                {
                    xOffset = 0;
                    yOffset += maxRowHeight + spacing;
                    maxRowHeight = 0;
                }

                if (yOffset + height > sheetHeight)
                {
                    sheets.Add(currentSheet);
                    currentSheet = new List<Rectangle3d>();
                    sheetStartX += sheetWidth + spacing;
                    xOffset = 0;
                    yOffset = 0;
                    maxRowHeight = 0;
                }

                Point3d newCorner = new Point3d(sheetStartX + xOffset, yOffset, 0);
                Plane newPlane = new Plane(newCorner, Vector3d.XAxis, Vector3d.YAxis);
                Rectangle3d placedBox = new Rectangle3d(newPlane, width, height);
                currentSheet.Add(placedBox);

                if (bboxToSurface.ContainsKey(bbox))
                {
                    Brep surface = bboxToSurface[bbox];
                    BoundingBox surfaceBbox = surface.GetBoundingBox(true);
                    Transform move = Transform.Translation(newCorner - surfaceBbox.Min);
                    Brep movedSurface = surface.DuplicateBrep();
                    movedSurface.Transform(move);
                    placedSurfaces.Add(movedSurface);
                }

                xOffset += width + spacing;
                maxRowHeight = Math.Max(maxRowHeight, height);
            }

            if (currentSheet.Count > 0)
                sheets.Add(currentSheet);

            for (int i = 0; i < sheets.Count; i++)
            {
                double sheetX = i * (sheetWidth + spacing);
                Rectangle3d sheetOutline = new Rectangle3d(Plane.WorldXY, new Point3d(sheetX, 0, 0), new Point3d(sheetX + sheetWidth, sheetHeight, 0));
                sheetOutlines.Add(sheetOutline);
            }

            DA.SetDataList(0, sheets.SelectMany(s => s));
            DA.SetDataList(1, sheetOutlines);
            DA.SetDataList(2, placedSurfaces);
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;
        protected override System.Drawing.Bitmap Icon => null; // Add an icon if desired
        public override Guid ComponentGuid => new Guid("b2d9a1ec-9c46-4d32-85bc-2a98e943e6b6");
    }
}