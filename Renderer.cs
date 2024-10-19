using System;
using System.Numerics;
using ClickableTransparentOverlay;
using ImGuiNET;

namespace menu
{
    public class OverlayRenderer : Overlay // Changed name to OverlayRenderer
    {
        // create a bool for each menu element
        public bool Godmode = false;
        public bool InfiniteAmmo = false;
        public bool Aimbot = false;
        public bool Teleport = false;
        public bool bullettp = false;
        public bool silentaim = false;
        //public bool autofire = false;
        public bool enemyteleport = false;

        protected override void Render()
        {
            ImGui.Begin("assault cube cheat - by @heyimlumi");

            ImGui.Checkbox("Godmode (APPLIES ON BOTS) (won't work online, restart your game)", ref Godmode);

            ImGui.Checkbox("Infinite Ammo (won't work online, restart your game)", ref InfiniteAmmo);

            ImGui.Text("------------------------------------------");

            ImGui.Checkbox("bullet tp (walmart version)", ref bullettp);
            //ImGui.Checkbox("auto-shoot", ref autofire);
            ImGui.Checkbox("Silent", ref silentaim);
            ImGui.Text("Makes bullet tp silent");

            ImGui.Text("------------------------------------------");

            ImGui.Checkbox("Aimbot (mouse2)", ref Aimbot);

            ImGui.Checkbox("Teleport to nearest enemy (mouse5)", ref Teleport);

            ImGui.Checkbox("Teleport enemies (mouse1)", ref enemyteleport);

        }
    }
}
