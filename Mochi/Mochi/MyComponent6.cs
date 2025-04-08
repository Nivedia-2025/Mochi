using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Rhino;
using Rhino.Geometry;

public class SegmentER : GH_Component
{
    public SegmentER()
      : base("SegmentER", "Segment",
          "Converts the strips of a non-planar geometry into planar segments",
          "Mochi", "Geometry")
    { }

    public override Guid ComponentGuid => new Guid("a2c6fa4e-0a4c-49b6-b8a5-7b90b3f3c779");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddBrepParameter("Strips", "S", "List of Brep strips", GH_ParamAccess.list);
        pManager.AddIntegerParameter("Index", "I", "Index of the strip to segment", GH_ParamAccess.item);
        pManager.AddBooleanParameter("U Direction", "U", "True = segment along V direction (U-based slices), False = segment along U (V-based slices)", GH_ParamAccess.item);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddBrepParameter("SelectedStrip", "S", "Selected Brep strip", GH_ParamAccess.item);
        pManager.AddBrepParameter("Triangulated", "T", "Triangulated segments from the strip", GH_ParamAccess.list);
    }

    protected override void SolveInstance(IGH_DataAccess DA)
    {
        List<Brep> strips = new List<Brep>();
        int index = 0;
        bool isUDirection = true;

        if (!DA.GetDataList(0, strips)) return;
        if (!DA.GetData(1, ref index)) return;
        if (!DA.GetData(2, ref isUDirection)) return;

        if (strips == null || strips.Count == 0 || index < 0 || index >= strips.Count)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid strip index or empty input.");
            return;
        }

        Brep strip = strips[index];
        List<Brep> triangles = isUDirection ? TriangulateInV(strip) : TriangulateInU(strip);

        DA.SetData(0, strip);
        DA.SetDataList(1, triangles);
    }

    private List<Brep> TriangulateInU(Brep strip)
    {
        return Triangulate(strip);
    }

    private List<Brep> TriangulateInV(Brep strip)
    {
        return Triangulate(strip);
    }

    private List<Brep> Triangulate(Brep strip)
    {
        List<Brep> triangleBreps = new List<Brep>();

        if (strip == null) return triangleBreps;

        foreach (BrepFace face in strip.Faces)
        {
            Mesh[] meshes = Mesh.CreateFromBrep(face.DuplicateFace(false), MeshingParameters.Default);
            if (meshes == null || meshes.Length == 0) continue;

            Mesh mesh = meshes[0];
            mesh.Faces.ConvertQuadsToTriangles();

            foreach (MeshFace mf in mesh.Faces)
            {
                if (mf.IsTriangle)
                {
                    Point3d a = mesh.Vertices[mf.A];
                    Point3d b = mesh.Vertices[mf.B];
                    Point3d c = mesh.Vertices[mf.C];

                    Brep triangle = Brep.CreateFromCornerPoints(a, b, c, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
                    if (triangle != null) triangleBreps.Add(triangle);
                }
            }
        }

        return triangleBreps;
    }
}
