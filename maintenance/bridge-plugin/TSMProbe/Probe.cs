using System;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;

namespace TSMProbe
{
    public class TSMProbePlugin : TerrariaPlugin
    {
        public override string Name => "TSMProbe";
        public override Version Version => new Version(1, 0, 0);
        public override string Author => "TSM";
        public TSMProbePlugin(Main game) : base(game) { }
        public override void Initialize() { TShock.Log.ConsoleInfo("[TSMProbe] 已启动（最小插件对照实验）"); }
    }
}
