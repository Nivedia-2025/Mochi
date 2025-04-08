using System;
using System.Collections.Generic;
using System.Drawing;

using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Mochi
{
  public class Packkit : GH_Component
  {
    /// <summary>
    /// Each implementation of GH_Component must provide a public 
    /// constructor without any arguments.
    /// Category represents the Tab in which the component will appear, 
    /// Subcategory the panel. If you use non-existing tab or panel names, 
    /// new tabs/panels will automatically be created.
    /// </summary>
    public Packkit()
      : base("FlappER", "flap",
        "Adds flap for unrolled geometry",
        "Mochi", "Geometry")
    {
    }

    /// <summary>
    /// Registers all the input parameters for this component.
    /// </summary>
    protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
    {
            pManager.AddCurveParameter("Curve", "C", "Planar input curve", GH_ParamAccess.item);
            pManager.AddNumberParameter("Offset Distance", "D", "Offset distance", GH_ParamAccess.item, 1.0);
            pManager.AddNumberParameter("Scale", "S", "Scale factor", GH_ParamAccess.item, 1.0);
            pManager.AddColourParameter("Color", "Col", "Mesh color", GH_ParamAccess.item, Color.LightBlue);
        }

    /// <summary>
    /// Registers all the output parameters for this component.
    /// </summary>
    protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
    {
            pManager.AddTextParameter("Status", "A", "Operation status", GH_ParamAccess.item);
            pManager.AddLineParameter("Lines", "L", "Original and offset lines", GH_ParamAccess.list);
            pManager.AddPointParameter("Centers", "C", "Centers of original lines", GH_ParamAccess.list);
            pManager.AddVectorParameter("Center Vectors", "V", "Vectors from original to offset line centers", GH_ParamAccess.list);
            pManager.AddLineParameter("Connection Lines", "CL", "Connection lines", GH_ParamAccess.list);
            pManager.AddMeshParameter("Surfaces", "S", "Planar surface meshes", GH_ParamAccess.list);
        }

    /// <summary>
    /// This is the method that actually does the work.
    /// </summary>
    /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
    /// to store data in output parameters.</param>
    protected override void SolveInstance(IGH_DataAccess DA)
    {
            Curve curve = null;
            double offsetDist = 0;
            double scale = 1;
            Color customColor = Color.LightBlue;

            if (!DA.GetData(0, ref curve)) return;
            if (!DA.GetData(1, ref offsetDist)) return;
            if (!DA.GetData(2, ref scale)) return;
            if (!DA.GetData(3, ref customColor)) return;

            Plane plane;
            if (!curve.TryGetPlane(out plane))
            {
                DA.SetData(0, "Invalid input! The curve must be planar.");
                return;
            }

            PolylineCurve polylineCurve = curve.ToPolyline(0, 0, 0, 0, 0, 0, 0, 0, true);

            if (polylineCurve == null || polylineCurve.PointCount < 2)
            {
                DA.SetData(0, "Failed to convert the curve to a polyline.");
                return;
            }

            Polyline polyline;
            if (!polylineCurve.TryGetPolyline(out polyline))
            {
                DA.SetData(0, "Failed to convert the PolylineCurve to a Polyline.");
                return;
            }

            List<Line> lineList = new List<Line>();
            List<Point3d> centerList = new List<Point3d>();
            List<Vector3d> centerVectorsList = new List<Vector3d>();
            List<Line> connectionLinesList = new List<Line>();
            List<Mesh> planarSurfacesList = new List<Mesh>();

            for (int i = 0; i < polyline.Count - 1; i++)
            {
                Line line = new Line(polyline[i], polyline[i + 1]);
                lineList.Add(line);

                Point3d originalCenter = new Point3d(
                  (line.From.X + line.To.X) / 2.0,
                  (line.From.Y + line.To.Y) / 2.0,
                  (line.From.Z + line.To.Z) / 2.0
                );
                centerList.Add(originalCenter);

                Vector3d direction = line.Direction;
                Vector3d perpendicularVector = Vector3d.CrossProduct(plane.ZAxis, direction);
                perpendicularVector.Unitize();
                perpendicularVector *= offsetDist;

                if (perpendicularVector * plane.ZAxis > 0)
                {
                    perpendicularVector = -perpendicularVector;
                }

                Point3d newStart = line.From + perpendicularVector;
                Point3d newEnd = line.To + perpendicularVector;

                Line newLine = new Line(newStart, newEnd);

                Point3d offsetCenter = new Point3d(
                  (newLine.From.X + newLine.To.X) / 2.0,
                  (newLine.From.Y + newLine.To.Y) / 2.0,
                  (newLine.From.Z + newLine.To.Z) / 2.0
                );

                Vector3d scaledStartVector = newStart - offsetCenter;
                Vector3d scaledEndVector = newEnd - offsetCenter;
                scaledStartVector *= scale;
                scaledEndVector *= scale;

                Point3d scaledStart = offsetCenter + scaledStartVector;
                Point3d scaledEnd = offsetCenter + scaledEndVector;

                newLine = new Line(scaledStart, scaledEnd);
                lineList.Add(newLine);

                Vector3d centerVector = offsetCenter - originalCenter;
                centerVectorsList.Add(centerVector);

                connectionLinesList.Add(new Line(line.From, scaledStart));
                connectionLinesList.Add(new Line(line.To, scaledEnd));

                Mesh planarSurface = new Mesh();
                planarSurface.Vertices.Add(line.From);
                planarSurface.Vertices.Add(line.To);
                planarSurface.Vertices.Add(scaledEnd);
                planarSurface.Vertices.Add(scaledStart);
                planarSurface.Faces.AddFace(0, 1, 2, 3);

                planarSurface.VertexColors.CreateMonotoneMesh(customColor);
                planarSurfacesList.Add(planarSurface);
            }

            DA.SetData(0, "Planar curve exploded into lines and their centers calculated!");
            DA.SetDataList(1, lineList);
            DA.SetDataList(2, centerList);
            DA.SetDataList(3, centerVectorsList);
            DA.SetDataList(4, connectionLinesList);
            DA.SetDataList(5, planarSurfacesList);
        }

    /// <summary>
    /// Provides an Icon for every component that will be visible in the User Interface.
    /// Icons need to be 24x24 pixels.
    /// You can add image files to your project resources and access them like this:
    /// return Resources.IconForThisComponent;
    /// </summary>
    protected override System.Drawing.Bitmap Icon => null;

    /// <summary>
    /// Each component must have a unique Guid to identify it. 
    /// It is vital this Guid doesn't change otherwise old ghx files 
    /// that use the old ID will partially fail during loading.
    /// </summary>
    public override Guid ComponentGuid => new Guid("e51ca5bc-54b0-40e2-b645-cd2ea7c8b4f5");
  }
}