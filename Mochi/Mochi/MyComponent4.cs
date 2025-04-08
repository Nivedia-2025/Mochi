using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Rhino;
using Rhino.Geometry;

namespace Mochi
{
    public class GeometryCleanER : GH_Component
    {
        public GeometryCleanER()
          : base("GeometryCleanER", "GeoClean",
              "Cleans Brep geometry by shrinking faces, merging coplanar faces, and simplifying edges.",
              "Mochi", "Geometry")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("Geometry", "G", "Input Brep geometry to clean", GH_ParamAccess.item);
            pManager.AddNumberParameter("Tolerance", "T", "Tolerance for simplification", GH_ParamAccess.item, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("Cleaned Brep", "C", "Resulting cleaned Brep geometry", GH_ParamAccess.item);
            pManager.AddTextParameter("Issues", "I", "Report of cleaning operations performed", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Brep geometry = null;
            double tolerance = RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;

            if (!DA.GetData(0, ref geometry)) return;
            if (!DA.GetData(1, ref tolerance)) return;

            if (geometry == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No Brep geometry was provided.");
                return;
            }

            Brep cleanBrep = geometry.DuplicateBrep();
            List<string> issues = new List<string>();

            // 1. Shrink Faces
            cleanBrep.Faces.ShrinkFaces();
            issues.Add("Shrank faces to remove small overlaps.");

            // 2. Merge Coplanar Faces
            int edgeCountBefore = cleanBrep.Edges.Count;
            cleanBrep.MergeCoplanarFaces(tolerance);
            int edgeCountAfter = cleanBrep.Edges.Count;
            if (edgeCountAfter < edgeCountBefore)
                issues.Add($"Merged coplanar faces. Reduced edge count from {edgeCountBefore} to {edgeCountAfter}.");

            // 3. Simplify edges (just a report, not actual edge replacement)
            int simplifiedCount = 0;
            for (int i = 0; i < cleanBrep.Edges.Count; i++)
            {
                Curve edge = cleanBrep.Edges[i].ToNurbsCurve();
                Curve simplified = edge.Simplify(CurveSimplifyOptions.All, tolerance, tolerance);

                if (simplified != null && !GeometryBase.GeometryEquals(edge, simplified))
                    simplifiedCount++;
            }

            if (simplifiedCount > 0)
                issues.Add($"Identified {simplifiedCount} simplified edges (informational only).");

            DA.SetData(0, cleanBrep);
            DA.SetData(1, string.Join("; ", issues));
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override System.Drawing.Bitmap Icon => null;

        public override Guid ComponentGuid => new Guid("C7A1C2DE-8888-4A89-903D-ABCDEF123456");
    }
}