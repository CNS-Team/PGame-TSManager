using System;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using HarmonyLib;

namespace tsmprobe3
{
    [ApiVersion(2, 1)]
    public class ProbePlugin : TerrariaPlugin
    {
        public override string Name => "tsmprobe3";
        public override Version Version => new Version(1, 0, 0);
        public override string Author => "TSM";
        public ProbePlugin(Main game) : base(game) { }
        public override void Initialize()
        {
            TShock.Log.ConsoleInfo("[V3] Initialize 进入");
            var h = new Harmony("probe3");
            try { h.PatchAll(typeof(ProbePlugin).Assembly); TShock.Log.ConsoleInfo("[P3] patchAll ok"); }
            catch (Exception ex) { TShock.Log.ConsoleInfo("[P3] patchAll 失败: " + ex.Message); }
            TShock.Log.ConsoleInfo("[V3] Initialize 完成");
        }
    }
    [HarmonyPatch(typeof(TShockAPI.Commands), "HandleCommand")]
    internal static class Probe3Patch
    {
        [HarmonyPrefix]
        private static void Prefix() { }
    }
}