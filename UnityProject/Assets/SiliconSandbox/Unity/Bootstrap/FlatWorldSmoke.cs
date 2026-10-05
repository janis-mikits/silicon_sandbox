using System;
using System.IO;
using SiliconSandbox.Contracts;
using UnityEngine;

namespace SiliconSandbox.Bootstrap
{
    // Flat-world development scene. The authored world editor arrives in later slices.
    public sealed class FlatWorldSmoke : MonoBehaviour
    {
        public const string FloorName = "Generated Floor Preview";

        private const string SmokeOutputVariable = "SILICON_SANDBOX_SMOKE_OUTPUT";

        private void Start()
        {
            var fixture = gameObject.AddComponent<AndCircuitFixture>();
            fixture.Build();
            var output = Environment.GetEnvironmentVariable(SmokeOutputVariable);
            if (string.IsNullOrEmpty(output))
                return;

            var valid = GetComponent<Collider>() != null && Camera.main != null &&
                fixture.Circuit != null && fixture.Y.Value == LogicBit.Zero &&
                GameObject.Find(AndCircuitFixture.GateObjectName) != null;
            File.WriteAllText(output, valid ? "PASS\n" : "FAIL\n");
            UnityEngine.Application.Quit(valid ? 0 : 1);
        }
    }
}
