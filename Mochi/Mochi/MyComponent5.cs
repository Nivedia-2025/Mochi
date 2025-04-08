using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Mochi
{
    public class StrippER : GH_Component
    {
        public StrippER()
          : base("StrippER", "Strip",
              "Converts a non planar geometry into planar strips in u and v direction",
              "Mochi", "Geometry")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("Brep", "B", "Input Brep geometry", GH_ParamAccess.item);
            pManager.AddIntegerParameter("U Divisions", "U", "Number of U divisions", GH_ParamAccess.item, 10);
            pManager.AddIntegerParameter("V Divisions", "V", "Number of V divisions", GH_ParamAccess.item, 10);
            pManager.AddBooleanParameter("Get V Strips", "VStrips", "Toggle to get V direction strips instead of U", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("Strips", "S", "Output planar strips", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Brep brep = null;
            int uDiv = 10;
            int vDiv = 10;
            bool getVStrips = false;

            if (!DA.GetData(0, ref brep)) return;
            if (!DA.GetData(1, ref uDiv)) return;
            if (!DA.GetData(2, ref vDiv)) return;
            if (!DA.GetData(3, ref getVStrips)) return;

            if (brep == null || uDiv < 1 || vDiv < 1) return;

            // Extract largest face
            double width, height;
            BrepFace largestFace = brep.Faces.OrderByDescending(f =>
            {
                f.GetSurfaceSize(out width, out height);
                return width * height;
            }).First();

            Surface surface = largestFace.DuplicateSurface();
            surface.SetDomain(0, new Interval(0, 1));
            surface.SetDomain(1, new Interval(0, 1));

            List<Brep> uStrips = GenerateStrips(surface, uDiv, true);
            List<Brep> vStrips = GenerateStrips(surface, vDiv, false);

            List<Brep> outputStrips = getVStrips ? vStrips : uStrips;
            DA.SetDataList(0, outputStrips);
        }

        private List<Brep> GenerateStrips(Surface surface, int divisions, bool isUDirection)
        {
            List<Brep> strips = new List<Brep>();
            Interval domain = isUDirection ? surface.Domain(0) : surface.Domain(1);
            double step = domain.Length / divisions;

            for (int i = 0; i < divisions; i++)
            {
                double t0 = domain.T0 + i * step;
                double t1 = domain.T0 + (i + 1) * step;

                Curve curve1 = isUDirection ? surface.IsoCurve(0, t0) : surface.IsoCurve(1, t0);
                Curve curve2 = isUDirection ? surface.IsoCurve(0, t1) : surface.IsoCurve(1, t1);

                if (curve1 != null && curve2 != null)
                {
                    Brep[] loft = Brep.CreateFromLoft(new List<Curve> { curve1, curve2 }, Point3d.Unset, Point3d.Unset, LoftType.Normal, false);
                    if (loft != null && loft.Length > 0)
                        strips.Add(loft[0]);
                }
            }

            return strips;
        }

        protected override System.Drawing.Bitmap Icon => null; // Add your custom icon here if needed

        public override Guid ComponentGuid => new Guid("0F1A3F24-42AE-4F68-8A0E-2534A9BA0C72");
    }
}