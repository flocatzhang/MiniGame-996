using OfficeHell.Systems;
using OfficeHell.View;

namespace OfficeHell.UI
{
    /// <summary>Everything the ui layer is allowed to read, handed over once at build time.</summary>
    public sealed class UIContext
    {
        public GameContext Game;
        public GameLoopDriver Driver;
        public AudioService Audio;
        public JuiceService Juice;
    }
}
