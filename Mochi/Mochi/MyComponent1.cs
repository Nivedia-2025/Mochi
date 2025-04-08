using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Mochi
{
    public class LabellER : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the LabellER class.
        /// </summary>
        public LabellER()
          : base("LabellER", "Label",
              "Numbers the segments",
              "Mochi", "Geometry")
        {
        }

        // 🔁✅ [KEEP THIS: INSIDE the LabellER class]
        protected override Bitmap Icon => null;

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Polygon Curves", "C", "Closed polygonal curves.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Label Distance", "D", "Distance from edge midpoint to label.", GH_ParamAccess.item, 1.0);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddLineParameter("Lines", "L", "Edge lines.", GH_ParamAccess.list);
            pManager.AddTextParameter("Labels", "T", "Edge labels.", GH_ParamAccess.list);
            pManager.AddPointParameter("Label Points", "P", "Label positions.", GH_ParamAccess.list);
            pManager.AddVectorParameter("Direction Vectors", "V", "Vectors pointing from edge to centroid.", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Curve> polygonCurves = new List<Curve>();
            double labelDistance = 1.0;
            DA.GetDataList(0, polygonCurves);
            DA.GetData(1, ref labelDistance);

            List<Polygon> polygons = new List<Polygon>();
            int polygonIndex = 1;
            foreach (var curve in polygonCurves)
            {
                if (curve.IsClosed)
                {
                    Polyline polyline;
                    if (!curve.TryGetPolyline(out polyline))
                    {
                        PolylineCurve polylineCurve = curve.ToPolyline(0.01, 0.1, 0.1, 10);
                        polyline = polylineCurve.ToPolyline();
                    }

                    List<Point3d> vertices = new List<Point3d>(polyline);
                    List<Edge> edges = new List<Edge>();
                    int edgeIndex = 1;

                    char polygonLetter = (char)('A' + (polygonIndex - 1));
                    string polygonName = polygonIndex.ToString() + "_" + polygonLetter.ToString();

                    for (int i = 0; i < vertices.Count - 1; i++)
                    {
                        string edgeLabel = polygonName + edgeIndex.ToString();
                        edges.Add(new Edge(vertices[i], vertices[i + 1], edgeLabel));
                        edgeIndex++;
                    }

                    polygons.Add(new Polygon(polygonName, edges));
                    polygonIndex++;
                }
            }

            AssignEdgeLabels(polygons);

            List<Line> lines = new List<Line>();
            List<string> labels = new List<string>();
            List<Point3d> labelPoints = new List<Point3d>();
            List<Vector3d> directionVectors = new List<Vector3d>();

            foreach (var polygon in polygons)
            {
                Point3d centroid = CalculateCentroid(polygon.Edges);

                foreach (var edge in polygon.Edges)
                {
                    lines.Add(new Line(edge.Start, edge.End));

                    Point3d midpoint = MidPoint(edge.Start, edge.End);
                    Vector3d direction = centroid - midpoint;
                    direction.Unitize();

                    Point3d labelPoint = midpoint + direction * labelDistance;

                    labels.Add(edge.Label);
                    labelPoints.Add(labelPoint);
                    directionVectors.Add(direction);
                }
            }

            DA.SetDataList(0, lines);
            DA.SetDataList(1, labels);
            DA.SetDataList(2, labelPoints);
            DA.SetDataList(3, directionVectors);
        }

        private Point3d MidPoint(Point3d a, Point3d b)
        {
            return new Point3d((a.X + b.X) / 2, (a.Y + b.Y) / 2, (a.Z + b.Z) / 2);
        }

        private Point3d CalculateCentroid(List<Edge> edges)
        {
            Point3d centroid = new Point3d(0, 0, 0);
            foreach (var edge in edges)
            {
                centroid += edge.Start;
                centroid += edge.End;
            }
            centroid /= (2 * edges.Count);
            return centroid;
        }

        private void AssignEdgeLabels(List<Polygon> polygons)
        {
            Dictionary<string, List<Tuple<Polygon, Edge>>> sharedEdges = new Dictionary<string, List<Tuple<Polygon, Edge>>>();

            foreach (var polygon in polygons)
            {
                int edgeIndex = 1;
                foreach (var edge in polygon.Edges)
                {
                    string key = edge.GetKey();

                    if (!sharedEdges.ContainsKey(key))
                        sharedEdges[key] = new List<Tuple<Polygon, Edge>>();

                    sharedEdges[key].Add(new Tuple<Polygon, Edge>(polygon, edge));
                    edge.Label = polygon.Name + edgeIndex.ToString();
                    edgeIndex++;
                }
            }

            foreach (var edgeGroup in sharedEdges.Values)
            {
                if (edgeGroup.Count > 1)
                {
                    edgeGroup.Sort((a, b) => string.Compare(a.Item1.Name, b.Item1.Name));
                    string sharedLabel = edgeGroup[0].Item2.Label;
                    foreach (var tuple in edgeGroup)
                    {
                        tuple.Item2.Label = sharedLabel;
                    }
                }
            }
        }

        // 🔁✅ [KEEP THIS: INSIDE the LabellER class]
        public override Guid ComponentGuid => new Guid("2A812E51-965F-4BC3-A143-290FF2FAE967");
    }

    public class Polygon
    {
        public string Name { get; set; }
        public List<Edge> Edges { get; set; }

        public Polygon(string name, List<Edge> edges)
        {
            Name = name;
            Edges = edges;
        }
    }

    public class Edge
    {
        public Point3d Start { get; set; }
        public Point3d End { get; set; }
        public string Label { get; set; }

        public Edge(Point3d start, Point3d end, string label)
        {
            Start = start;
            End = end;
            Label = label;
        }

        public bool IsShared(Edge other)
        {
            double tolerance = 0.0001;
            return (PointsEqual(Start, other.Start, tolerance) && PointsEqual(End, other.End, tolerance)) ||
                   (PointsEqual(Start, other.End, tolerance) && PointsEqual(End, other.Start, tolerance));
        }

        private bool PointsEqual(Point3d a, Point3d b, double tolerance)
        {
            return a.DistanceTo(b) < tolerance;
        }

        public string GetKey()
        {
            return (Start.X < End.X || (Start.X == End.X && Start.Y < End.Y))
                ? Start.ToString() + "-" + End.ToString()
                : End.ToString() + "-" + Start.ToString();
        }
    }
}