using System;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using HarmonyLib;

namespace tsmprobe2
{
    [ApiVersion(2, 1)]
    public class ProbePlugin : TerrariaPlugin
    {
        public override string Name => "tsmprobe2";
        public override Version Version => new Version(1, 0, 0);
        public override string Author => "TSM";
        public ProbePlugin(Main game) : base(game) { }
        public override void Initialize()
        {
            TShock.Log.ConsoleInfo("[V2] Initialize 进入");
            var h = new Harmony("probe2");
            TShock.Log.ConsoleInfo("[P2] Harmony 实例已创建（未打任何 patch）");
            TShock.Log.ConsoleInfo("[V2] Initialize 完成");
        }
    }

}