using System.Collections.Generic;
using UnityEngine;

namespace Cyberpunk.Traffic.Trains
{
    /// <summary>
    /// Utility class for assembling continuous CyberTrainPath spline curves from
    /// scene rail gameobjects (both straight beams and 90-degree curved beams).
    /// </summary>
    public static class CyberTrackBuilder
    {
        private class RailNode
        {
            public Transform tf;
            public string name;
            public Vector3 p0; // world start (local 0,0,0)
            public Vector3 p1; // world end (local -25, 0, 0 or -25, 0, -25)
            public bool isCurve;
            public List<RailConn> connections = new List<RailConn>();
        }

        private class RailConn
        {
            public RailNode other;
            public bool myEndIsP1;
            public bool otherEndIsP1;
        }

        /// <summary>
        /// Builds a continuous CyberTrainPath for all connected rails around a given Y height or seed transform.
        /// </summary>
        public static CyberTrainPath BuildPathFromSeed(Transform seedRail, Transform railsRoot, float curveSubdivisions = 12)
        {
            if (seedRail == null || railsRoot == null) return null;

            List<RailNode> allNodes = ExtractRailNodes(railsRoot);
            RailNode seedNode = allNodes.Find(n => n.tf == seedRail);
            if (seedNode == null) return null;

            BuildAdjacencyGraph(allNodes, 1.5f);

            // Extract connected component containing seed
            HashSet<RailNode> component = new HashSet<RailNode>();
            Queue<RailNode> q = new Queue<RailNode>();
            q.Enqueue(seedNode);
            component.Add(seedNode);

            while (q.Count > 0)
            {
                var cur = q.Dequeue();
                foreach (var conn in cur.connections)
                {
                    if (!component.Contains(conn.other))
                    {
                        component.Add(conn.other);
                        q.Enqueue(conn.other);
                    }
                }
            }

            // Find an endpoint (degree 1 node) to start path from
            RailNode startNode = null;
            foreach (var n in component)
            {
                if (n.connections.Count == 1)
                {
                    startNode = n;
                    break;
                }
            }

            // If it's a closed loop without degree 1, pick seedNode
            if (startNode == null) startNode = seedNode;

            return TraverseAndSample(startNode, component, (int)curveSubdivisions);
        }

        private static List<RailNode> ExtractRailNodes(Transform root)
        {
            List<RailNode> nodes = new List<RailNode>();
            for (int i = 0; i < root.childCount; i++)
            {
                Transform t = root.GetChild(i);
                if (!t.name.Contains("Rail_Straight") && !t.name.Contains("Rail_Curve")) continue;

                bool isCurve = t.name.Contains("Rail_Curve");
                Vector3 p0 = t.position;
                Vector3 p1 = isCurve
                    ? t.TransformPoint(new Vector3(-25f, 0f, -25f))
                    : t.TransformPoint(new Vector3(-25f, 0f, 0f));

                nodes.Add(new RailNode { tf = t, name = t.name, p0 = p0, p1 = p1, isCurve = isCurve });
            }
            return nodes;
        }

        private static void BuildAdjacencyGraph(List<RailNode> nodes, float threshold)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                for (int j = i + 1; j < nodes.Count; j++)
                {
                    var a = nodes[i];
                    var b = nodes[j];

                    if (Vector3.Distance(a.p0, b.p0) < threshold)
                    {
                        a.connections.Add(new RailConn { other = b, myEndIsP1 = false, otherEndIsP1 = false });
                        b.connections.Add(new RailConn { other = a, myEndIsP1 = false, otherEndIsP1 = false });
                    }
                    else if (Vector3.Distance(a.p0, b.p1) < threshold)
                    {
                        a.connections.Add(new RailConn { other = b, myEndIsP1 = false, otherEndIsP1 = true });
                        b.connections.Add(new RailConn { other = a, myEndIsP1 = true, otherEndIsP1 = false });
                    }
                    else if (Vector3.Distance(a.p1, b.p0) < threshold)
                    {
                        a.connections.Add(new RailConn { other = b, myEndIsP1 = true, otherEndIsP1 = false });
                        b.connections.Add(new RailConn { other = a, myEndIsP1 = false, otherEndIsP1 = true });
                    }
                    else if (Vector3.Distance(a.p1, b.p1) < threshold)
                    {
                        a.connections.Add(new RailConn { other = b, myEndIsP1 = true, otherEndIsP1 = true });
                        b.connections.Add(new RailConn { other = a, myEndIsP1 = true, otherEndIsP1 = true });
                    }
                }
            }
        }

        private static CyberTrainPath TraverseAndSample(RailNode startNode, HashSet<RailNode> component, int curveSteps)
        {
            List<Vector3> sampledPoints = new List<Vector3>();
            HashSet<RailNode> visited = new HashSet<RailNode>();

            RailNode current = startNode;
            // If connection is at P1, the outer terminus end is P0, so we enter at P0
            bool enterAtP0 = current.connections.Count == 0 || current.connections[0].myEndIsP1;

            while (current != null && visited.Count < component.Count)
            {
                visited.Add(current);

                if (!current.isCurve)
                {
                    Vector3 entry = enterAtP0 ? current.p0 : current.p1;
                    Vector3 exit = enterAtP0 ? current.p1 : current.p0;

                    if (sampledPoints.Count == 0) sampledPoints.Add(entry);
                    sampledPoints.Add(exit);
                }
                else
                {
                    // 90-degree circular curve of radius 25
                    for (int s = 0; s <= curveSteps; s++)
                    {
                        float t = (float)s / curveSteps;
                        if (!enterAtP0) t = 1f - t; // Reverse arc parameter if entering at P1

                        float rad = (Mathf.PI * 0.5f) * (1f + t);
                        Vector3 localPt = new Vector3(25f * Mathf.Cos(rad), 0f, -25f + 25f * Mathf.Sin(rad));
                        Vector3 worldPt = current.tf.TransformPoint(localPt);

                        if (sampledPoints.Count == 0 || s > 0)
                        {
                            sampledPoints.Add(worldPt);
                        }
                    }
                }

                // Advance to next node
                RailNode next = null;
                bool nextEnterAtP0 = false;

                foreach (var conn in current.connections)
                {
                    if (!visited.Contains(conn.other) && component.Contains(conn.other))
                    {
                        next = conn.other;
                        // If connection meets other at P1, we enter other at P1 (nextEnterAtP0 = false)
                        nextEnterAtP0 = !conn.otherEndIsP1;
                        break;
                    }
                }

                current = next;
                enterAtP0 = nextEnterAtP0;
            }

            CyberTrainPath path = new CyberTrainPath();
            path.InitializeFromPoints(sampledPoints);
            return path;
        }
    }
}
