using System;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;


namespace tsmprobe1
{
    [ApiVersion(2, 1)]
    public class ProbePlugin : TerrariaPlugin
    {
        public override string Name => "tsmprobe1";
        public override Version Version => new Version(1, 0, 0);
        public override string Author => "TSM";
        public ProbePlugin(Main game) : base(game) { }
        public override void Initialize()
        {
            TShock.Log.ConsoleInfo("[V1] Initialize 进入");
            TShock.Log.ConsoleInfo("[P1] 无 Harmony 依赖");
            TShock.Log.ConsoleInfo("[V1] Initialize 完成");
        }
    }

}