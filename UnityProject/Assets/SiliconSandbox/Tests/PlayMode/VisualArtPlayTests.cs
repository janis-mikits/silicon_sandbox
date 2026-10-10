using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SiliconSandbox.Authoring;
using SiliconSandbox.Bootstrap;
using SiliconSandbox.Contracts;
using SiliconSandbox.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SiliconSandbox.Tests.PlayMode
{
    public sealed class VisualArtPlayTests
    {
        [UnityTest]
        public IEnumerator FailedReconnectShowsFeedbackAndAllowsBreakThenRetry()
        {
            SceneManager.LoadScene("PlayableWorld");
            yield return null;
            var bootstrap=GameObject.Find(FlatWorldSmoke.FloorName).GetComponent<PlayableWorldBootstrap>();
            var session=bootstrap.Session;
            session.PlaceComponent(BuiltInPinCatalog.Source,new GridCell(6,1,6),GridOrientation.Default);
            session.PlaceComponent(BuiltInPinCatalog.SrFlipFlop,new GridCell(8,1,6),GridOrientation.Default);
            session.PlaceComponent(BuiltInPinCatalog.And,new GridCell(10,1,8),GridOrientation.Default);
            var source=session.Design.Components[0];var sr=session.Design.Components[1];var gate=session.Design.Components[2];
            var output=JoinMember.ComponentPin(source.Id,source.PinIds["OUT"]);
            var input=JoinMember.ComponentPin(gate.Id,gate.PinIds["A"]);
            session.ConnectPins(output,JoinMember.ComponentPin(sr.Id,sr.PinIds["R"]));
            var route=session.Design.Topology.Connectors[0];
            session.BreakSpan(route.Id,route.Spans[route.Spans.Count-1].Id);
            yield return null;
            var before=session.Design;var revision=session.Revision;
            var interaction=bootstrap.Interaction;
            bootstrap.Inventory.SelectHotbar(1);
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var type=interaction.GetType();
            var hovered=type.GetField("hovered",flags);
            var click=type.GetMethod("RightClick",flags);
            var preview=type.GetMethod("UpdateWirePreview",flags);
            hovered.SetValue(interaction,GameObject.Find("Pin "+source.PinIds["OUT"].ToString("D")).GetComponent<WorldSelectablePart>());
            click.Invoke(interaction,null);
            var target=GameObject.Find("Pin "+gate.PinIds["A"].ToString("D")).GetComponent<WorldSelectablePart>();
            hovered.SetValue(interaction,target);
            type.GetField("hasHit",flags).SetValue(interaction,true);
            preview.Invoke(interaction,null);
            var cached=type.GetField("wirePreviewRoot",flags).GetValue(interaction);
            Assert.That(cached,Is.Not.Null);
            preview.Invoke(interaction,null);
            Assert.That(type.GetField("wirePreviewRoot",flags).GetValue(interaction),Is.SameAs(cached),
                "A failed preview must be cached, not searched again every frame.");
            Assert.That(type.GetField("invalidClick",flags).GetValue(interaction),Is.Null,
                "Hovering an invalid preview must not repeatedly make error sounds.");
            click.Invoke(interaction,null);
            Assert.That(session.Design,Is.SameAs(before));
            Assert.That(session.Revision,Is.EqualTo(revision));
            Assert.That(type.GetField("wireStart",flags).GetValue(interaction),Is.Null);
            Assert.That((float)typeof(WorldSelectablePart).GetField("invalidUntil",flags).GetValue(target),
                Is.GreaterThan(Time.unscaledTime));
            var clip=(AudioClip)type.GetField("invalidClick",flags).GetValue(interaction);
            Assert.That(clip,Is.Not.Null);
            var samples=new float[clip.samples];Assert.That(clip.GetData(samples,0),Is.True);
            Assert.That(System.Array.Exists(samples,v=>Mathf.Abs(v)>0.01f),Is.True);
            Assert.That(interaction.GetComponent<AudioSource>().volume,Is.EqualTo(0.15f));
            var frame=Time.frameCount;
            yield return null;
            Assert.That(Time.frameCount,Is.GreaterThan(frame));
            var remaining=new List<ConnectorRoute>(session.Design.Topology.Connectors);
            foreach(var old in remaining)session.BreakConnector(old.Id);
            yield return null;
            hovered.SetValue(interaction,GameObject.Find("Pin "+source.PinIds["OUT"].ToString("D")).GetComponent<WorldSelectablePart>());
            click.Invoke(interaction,null);
            hovered.SetValue(interaction,GameObject.Find("Pin "+gate.PinIds["A"].ToString("D")).GetComponent<WorldSelectablePart>());
            click.Invoke(interaction,null);
            Assert.That(session.Design.Topology.Connectors.Count,Is.EqualTo(1));
            Assert.That(session.Revision,Is.GreaterThan(revision));
            Assert.That(type.GetField("wireStart",flags).GetValue(interaction),Is.Null);
        }

        [UnityTest]
        public IEnumerator PassingWireLeavesNeighborPinTargetableAndConnectable()
        {
            SceneManager.LoadScene("PlayableWorld");
            yield return null;
            var session=GameObject.Find(FlatWorldSmoke.FloorName).GetComponent<PlayableWorldBootstrap>().Session;
            session.PlaceComponent(BuiltInPinCatalog.SrFlipFlop,new GridCell(6,1,6),GridOrientation.Default);
            session.PlaceComponent(BuiltInPinCatalog.And,new GridCell(8,1,6),GridOrientation.Default);
            var sr=session.Design.Components[0];var gate=session.Design.Components[1];
            session.ConnectPins(JoinMember.ComponentPin(sr.Id,sr.PinIds["Q"]),
                JoinMember.ComponentPin(gate.Id,gate.PinIds["B"]));
            yield return null;
            Physics.SyncTransforms();
            Assert.That(Physics.Raycast(new Vector3(7.375f,1.75f,6.25f),Vector3.left,out var hit,0.5f),Is.True);
            var selected=hit.collider.GetComponent<WorldSelectablePart>();
            Assert.That(selected,Is.Not.Null);
            Assert.That(selected.Kind,Is.EqualTo(WorldPartKind.ComponentPin));
            Assert.That(selected.PartId,Is.EqualTo(sr.PinIds["Q_bar"]),
                "The nearby wire must not intercept a front-on pin target.");
            session.ConnectPins(JoinMember.ComponentPin(sr.Id,sr.PinIds["Q_bar"]),
                JoinMember.ComponentPin(gate.Id,gate.PinIds["A"]));
            yield return null;
            Assert.That(session.Design.Topology.Connectors.Count,Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator SourceMarkingShowsDrivenZeroOneXAndZOnItsSurface()
        {
            SceneManager.LoadScene("PlayableWorld");
            yield return null;
            var art = Resources.Load<OneBitVisualArt>("SiliconSandboxVisualArt");
            Assert.That(art, Is.Not.Null);
            Assert.That(art.IsComplete, Is.True);
            Assert.That(art.SourceBody.bounds.size.x, Is.EqualTo(0.78f).Within(0.001f));
            var session = GameObject.Find(FlatWorldSmoke.FloorName)
                .GetComponent<PlayableWorldBootstrap>().Session;
            session.PlaceComponent(BuiltInPinCatalog.Source,
                new GridCell(6, 1, 6), GridOrientation.Default);
            var source = session.Design.Components[0];
            yield return null;

            var body = GameObject.Find("Component " + source.Id.ToString("D"));
            Assert.That(body, Is.Not.Null);
            Assert.That(body.GetComponent<MeshFilter>().sharedMesh,
                Is.SameAs(art.SourceBody));
            Assert.That(body.GetComponent<BoxCollider>().size.x,
                Is.EqualTo(0.78f).Within(0.001f));
            Assert.That(body.transform.Find("Component label"), Is.Null,
                "Built-in identity should be on the block, not floating above it.");
            AssertValue(source.Id.ToString("D"), art.SourceZero);

            session.ToggleSource(source.Id);
            yield return null;
            AssertValue(source.Id.ToString("D"), art.SourceOne);
            session.ConfigureSource(source.Id, LogicBit.X, false);
            yield return null;
            AssertValue(source.Id.ToString("D"), art.SourceX);
            session.ConfigureSource(source.Id, LogicBit.Z, false);
            yield return null;
            AssertValue(source.Id.ToString("D"), art.SourceZ);
            session.ToggleSource(source.Id);
            yield return null;
            AssertValue(source.Id.ToString("D"), art.SourceZero);
        }

        [UnityTest]
        public IEnumerator ImportedPinsAndWiresRetainExactSelectableParts()
        {
            SceneManager.LoadScene("PlayableWorld");
            yield return null;
            var art = Resources.Load<OneBitVisualArt>("SiliconSandboxVisualArt");
            var session = GameObject.Find(FlatWorldSmoke.FloorName)
                .GetComponent<PlayableWorldBootstrap>().Session;
            Assert.That(art.WireStraight.bounds.size.x,
                Is.EqualTo(0.125f).Within(0.001f));
            Assert.That(art.Pin.bounds.max.y,
                Is.EqualTo(0.0625f).Within(0.001f));
            session.PlaceComponent(BuiltInPinCatalog.Source,
                new GridCell(6, 1, 6), GridOrientation.Default);
            var source = session.Design.Components[0];
            session.PlaceWireStub(JoinMember.ComponentPin(source.Id,
                source.PinIds["OUT"]), new GridCell(8, 1, 6));
            var route = session.Design.Topology.Connectors[0];
            yield return null;

            var pinFound = false;
            var spanFound = false;
            foreach (var part in Object.FindObjectsByType<WorldSelectablePart>(
                         FindObjectsSortMode.None))
            {
                if (part.Kind == WorldPartKind.ComponentPin &&
                    part.OwnerId == source.Id &&
                    part.PartId == source.PinIds["OUT"])
                {
                    pinFound = true;
                    Assert.That(part.GetComponent<MeshFilter>().sharedMesh,
                        Is.SameAs(art.Pin));
                    Assert.That(part.GetComponent<CapsuleCollider>().radius,
                        Is.EqualTo(0.06875f).Within(0.001f));
                }
                if (part.Kind == WorldPartKind.ConnectorSpan &&
                    part.OwnerId == route.Id &&
                    part.GetComponent<MeshFilter>() != null && part.GetComponent<Renderer>().enabled)
                {
                    spanFound = true;
                    var capsule = part.GetComponent<CapsuleCollider>();
                    if (capsule != null)
                        Assert.That(capsule.radius, Is.EqualTo(0.0625f).Within(0.001f));
                    else Assert.That(part.GetComponent<MeshCollider>(), Is.Not.Null);
                }
            }
            Assert.That(pinFound, Is.True);
            Assert.That(spanFound, Is.True);
        }

        [UnityTest]
        public IEnumerator OpposingDriversKeepTheirOwnMarkingsWhileTheWireIsX()
        {
            SceneManager.LoadScene("PlayableWorld");
            yield return null;
            var session = GameObject.Find(FlatWorldSmoke.FloorName)
                .GetComponent<PlayableWorldBootstrap>().Session;
            var art = Resources.Load<OneBitVisualArt>("SiliconSandboxVisualArt");
            session.PlaceComponent(BuiltInPinCatalog.Source,
                new GridCell(6, 1, 6), GridOrientation.Default);
            session.PlaceComponent(BuiltInPinCatalog.Source,
                new GridCell(8, 1, 6),
                GridOrientation.Default.ClockwiseYaw().ClockwiseYaw());
            var first = session.Design.Components[0];
            var second = session.Design.Components[1];
            session.ConnectPins(JoinMember.ComponentPin(first.Id, first.PinIds["OUT"]),
                JoinMember.ComponentPin(second.Id, second.PinIds["OUT"]));
            // Sources start off and drive 0. Turning on only the first makes
            // literal opposing 1/0 drivers; initialOn is an authored restart
            // setting and does not toggle an already-running source.
            session.ToggleSource(first.Id);
            yield return null;

            var route = session.Design.Topology.Connectors[0];
            Assert.That(session.Inspector.InspectConnector(route.Id).Value,
                Is.EqualTo(LogicBit.X),
                "Conflicting 1 and 0 drivers resolve to X on their shared wire.");
            AssertValue(first.Id.ToString("D"), art.SourceOne);
            AssertValue(second.Id.ToString("D"), art.SourceZero);
        }

        [UnityTest]
        public IEnumerator ConnectedWireEntersThePinOnItsAxis()
        {
            SceneManager.LoadScene("PlayableWorld");
            yield return null;
            var session = GameObject.Find(FlatWorldSmoke.FloorName)
                .GetComponent<PlayableWorldBootstrap>().Session;
            var art = Resources.Load<OneBitVisualArt>("SiliconSandboxVisualArt");
            Assert.That(art.Pin.bounds.size.x, Is.EqualTo(0.1375f).Within(0.0001f));
            Assert.That(art.Pin.bounds.size.x / art.WireStraight.bounds.size.x,
                Is.EqualTo(1.1f).Within(0.0001f));
            session.PlaceComponent(BuiltInPinCatalog.Source,
                new GridCell(6, 1, 6), GridOrientation.Default);
            var source = session.Design.Components[0];
            session.PlaceWireStub(JoinMember.ComponentPin(source.Id,
                source.PinIds["OUT"]), new GridCell(8, 1, 6));
            yield return null;
            // Literal V1 OUT position, independent of the renderer's placement helper.
            var expected = new Vector3(7f, 1.25f, 6.25f);
            var pin = GameObject.Find("Pin " + source.PinIds["OUT"].ToString("D"));
            Assert.That(Vector3.Distance(pin.transform.position, expected), Is.LessThan(0.00001f));
            var found = false;
            foreach (var part in Object.FindObjectsByType<WorldSelectablePart>(FindObjectsSortMode.None))
            {
                if (part.Kind != WorldPartKind.ConnectorSpan) continue;
                var filter = part.GetComponent<MeshFilter>();
                if (filter == null || !part.GetComponent<Renderer>().enabled) continue;
                var ring = new HashSet<Vector3>();
                foreach (var vertex in filter.sharedMesh.vertices)
                {
                    var point = part.transform.TransformPoint(vertex);
                    if (Mathf.Abs(point.x - expected.x) < 0.00001f &&
                        Mathf.Abs(point.y - expected.y) < 0.063f &&
                        Mathf.Abs(point.z - expected.z) < 0.063f) ring.Add(point);
                }
                if (ring.Count < 12) continue;
                var center = Vector3.zero;
                foreach (var point in ring) center += point;
                center /= ring.Count;
                Assert.That(Vector3.Distance(center, expected), Is.LessThan(0.0001f));
                foreach (var point in ring)
                    Assert.That(Vector3.Distance(point, center), Is.EqualTo(0.0625f).Within(0.0001f));
                found = true;
            }
            Assert.That(found, Is.True, "A centered circular wire end must enter the pin.");
            var ringObject = GameObject.Find("Connector " +
                session.Design.Topology.Connectors[0].Id.ToString("D")).transform.Find("Identity ring");
            Assert.That(ringObject, Is.Not.Null);
            Assert.That(Vector3.Distance(ringObject.position, expected + Vector3.right * 0.14f),
                Is.LessThan(0.00001f), "The identity ring must sit on the actual first wire leg.");
            // Offscreen render of the tested Unity scene, not a desktop capture.
            var cameraObject = new GameObject("Wire fit review camera");
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.transform.position = expected + new Vector3(0.9f, 0.55f, -0.9f);
            cameraObject.transform.LookAt(expected + Vector3.right * 0.25f);
            camera.orthographic = true; camera.orthographicSize = 0.65f;
            var target = new RenderTexture(1000, 800, 24);
            var previous = RenderTexture.active;
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            var image = new Texture2D(1000, 800, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1000, 800), 0, 0); image.Apply();
            // Uncompressed TGA avoids adding Unity's optional ImageConversion module.
            using (var file = new System.IO.BinaryWriter(System.IO.File.Create(
                UnityEngine.Application.dataPath + "/SiliconSandbox/Art/Review~/unity-wire-fit.tga")))
            {
                file.Write(new byte[] { 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
                file.Write((ushort)1000); file.Write((ushort)800);
                file.Write((byte)24); file.Write((byte)0);
                foreach (var pixel in image.GetPixels32())
                { file.Write(pixel.b); file.Write(pixel.g); file.Write(pixel.r); }
            }
            camera.targetTexture = null; RenderTexture.active = previous;
            Object.Destroy(image); Object.Destroy(target); Object.Destroy(cameraObject);
        }

        [UnityTest]
        public IEnumerator ExactRouteHasAlignedPinFacesAndMatchingMiterSeams()
        {
            SceneManager.LoadScene("PlayableWorld");
            yield return null;
            var session=GameObject.Find(FlatWorldSmoke.FloorName).GetComponent<PlayableWorldBootstrap>().Session;
            session.PlaceComponent(BuiltInPinCatalog.Source,new GridCell(6,1,6),GridOrientation.Default);
            session.PlaceComponent(BuiltInPinCatalog.And,new GridCell(10,1,6),GridOrientation.Default);
            var source=session.Design.Components[0];var gate=session.Design.Components[1];
            session.ConnectPins(JoinMember.ComponentPin(source.Id,source.PinIds["OUT"]),
                JoinMember.ComponentPin(gate.Id,gate.PinIds["B"]));
            yield return null;
            var route=session.Design.Topology.Connectors[0];
            var nodes=new Dictionary<System.Guid,RouteNode>();
            foreach(var n in route.Nodes)nodes[n.Id]=n;
            var rings=new Dictionary<Vector3,List<Vector3[]>>();
            foreach(var span in route.Spans)
            {
                var a=nodes[span.FromNodeId];var b=nodes[span.ToNodeId];
                var from=new Vector3(a.Cell.X+a.PointQ.X*0.25f,a.Cell.Y+a.PointQ.Y*0.25f,a.Cell.Z+a.PointQ.Z*0.25f);
                var to=new Vector3(b.Cell.X+b.PointQ.X*0.25f,b.Cell.Y+b.PointQ.Y*0.25f,b.Cell.Z+b.PointQ.Z*0.25f);
                if(from==to)continue;
                Assert.That(Vector3.Distance(from,to),Is.GreaterThanOrEqualTo(0.25f));
                var item=GameObject.Find("Exact span "+span.Id.ToString("D"));
                Assert.That(item,Is.Not.Null);
                var vertices=item.GetComponent<MeshFilter>().sharedMesh.vertices;
                Assert.That(vertices.Length,Is.EqualTo(24));
                for(var end=0;end<2;end++)
                {
                    var center=Vector3.zero;var ring=new Vector3[12];
                    for(var k=0;k<12;k++){ring[k]=item.transform.TransformPoint(vertices[end*12+k]);center+=ring[k];}
                    center/=12;
                    var expected=end==0?from:to;
                    Assert.That(Vector3.Distance(center,expected),Is.LessThan(0.00001f));
                    if(!rings.ContainsKey(expected))rings[expected]=new List<Vector3[]>();
                    rings[expected].Add(ring);
                }
            }
            foreach(var pair in rings)
                if(pair.Value.Count==2)
                    foreach(var vertex in pair.Value[0])
                    {
                        var nearest=float.MaxValue;
                        foreach(var other in pair.Value[1])nearest=Mathf.Min(nearest,Vector3.Distance(vertex,other));
                        Assert.That(nearest,Is.LessThan(0.00001f),"Adjacent spans must share the same miter ring.");
                    }
            foreach(var expected in new[]{new Vector3(7f,1.25f,6.25f),new Vector3(10f,1.75f,6.25f)})
            {
                Assert.That(rings[expected].Count,Is.EqualTo(1));
                foreach(var vertex in rings[expected][0])
                {
                    Assert.That(vertex.x,Is.EqualTo(expected.x).Within(0.00001f));
                    Assert.That(Vector3.Distance(vertex,expected),Is.EqualTo(0.0625f).Within(0.00001f));
                }
            }
        }

        [Test]
        public void EveryDirectionalVariantHasExactlyItsRequestedCircularPorts()
        {
            var art = Resources.Load<OneBitVisualArt>("SiliconSandboxVisualArt");
            var axes = new[] { Vector3.right, Vector3.left, Vector3.up,
                Vector3.down, Vector3.forward, Vector3.back };
            Assert.That(art.WireVariants.Length, Is.EqualTo(64));
            for (var mask = 1; mask < 64; mask++)
            {
                Assert.That(art.WireVariants[mask], Is.Not.Null, "mask " + mask);
                var mesh = art.WireVariants[mask];
                for (var bit = 0; bit < 6; bit++)
                {
                    var ring = new HashSet<Vector3>();
                    foreach (var v in mesh.vertices)
                        if (Mathf.Abs(Vector3.Dot(v, axes[bit]) - 0.5f) < 0.00001f) ring.Add(v);
                    if ((mask & (1 << bit)) == 0)
                    { Assert.That(ring.Count, Is.Zero, "unexpected port " + mask + "/" + bit); continue; }
                    Assert.That(ring.Count, Is.EqualTo(12), "mask/port " + mask + "/" + bit);
                    var center = Vector3.zero;
                    foreach (var v in ring) center += v;
                    center /= 12;
                    Assert.That(Vector3.Distance(center, axes[bit] * 0.5f), Is.LessThan(0.00001f));
                    foreach (var v in ring)
                        Assert.That(Vector3.Distance(v, center), Is.EqualTo(0.0625f).Within(0.00001f));
                }
            }
        }

        [Test]
        public void SeparateCrossingLanesHaveClearanceInEveryPlane()
        {
            var center = new QuarterPoint(2, 2, 2);
            var cell = new GridCell(5, 1, 5);
            for (var a = 0; a < 4; a++)
                for (var b = a + 1; b < 4; b++)
                {
                    var delta = WireMeshGeometry.NodePosition(cell, center, b) -
                        WireMeshGeometry.NodePosition(cell, center, a);
                    // Perpendicular line pairs in XY, XZ, YZ are separated
                    // along Z, Y, X respectively; all exceed the wire diameter.
                    Assert.That(Mathf.Abs(delta.x), Is.GreaterThan(0.125f));
                    Assert.That(Mathf.Abs(delta.y), Is.GreaterThan(0.125f));
                    Assert.That(Mathf.Abs(delta.z), Is.GreaterThan(0.125f));
                }
        }

        [Test]
        public void ContinuousMiterIsClosedForEveryOrderedRightAngleTurn()
        {
            var axes = new[] { Vector3.right, Vector3.left, Vector3.up,
                Vector3.down, Vector3.forward, Vector3.back };
            foreach (var incoming in axes)
                foreach (var outgoing in axes)
                {
                    if (Mathf.Abs(Vector3.Dot(incoming, outgoing)) > 0.01f) continue;
                    var mesh = WireMeshGeometry.Path(new[] { -incoming * 0.5f,
                        Vector3.zero, outgoing * 0.5f });
                    Assert.That(mesh.vertexCount, Is.EqualTo(36));
                    var edges = new Dictionary<string, int>();
                    var t = mesh.triangles;
                    for (var i = 0; i < t.Length; i += 3)
                        for (var k = 0; k < 3; k++)
                        {
                            var a = t[i + k]; var b = t[i + (k + 1) % 3];
                            var key = Mathf.Min(a,b) + ":" + Mathf.Max(a,b);
                            edges[key] = edges.TryGetValue(key, out var n) ? n + 1 : 1;
                        }
                    foreach (var count in edges.Values) Assert.That(count, Is.EqualTo(2));
                    for (var i = 0; i < 12; i++)
                    {
                        Assert.That(Vector3.Dot(mesh.vertices[i], incoming), Is.EqualTo(-0.5f).Within(0.00001f));
                        Assert.That(Vector3.Dot(mesh.vertices[24+i], outgoing), Is.EqualTo(0.5f).Within(0.00001f));
                    }
                    Object.DestroyImmediate(mesh);
                }
        }

        private static void AssertValue(string sourceId, Mesh expected)
        {
            var value = GameObject.Find("Source value " + sourceId);
            var body = GameObject.Find("Component " + sourceId);
            Assert.That(value, Is.Not.Null);
            Assert.That(body, Is.Not.Null);
            Assert.That(value.transform.parent, Is.SameAs(body.transform));
            Assert.That(value.GetComponent<MeshFilter>().sharedMesh,
                Is.SameAs(expected));
            Assert.That(value.GetComponent<Collider>(), Is.Null,
                "The decoration must not steal a pin or body target.");
        }
    }
}
