using menu;
using Swed32;
using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;
using ClickableTransparentOverlay;
using System.Threading;
using System.Diagnostics;

class Program
{
    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);
    const int VK_RBUTTON = 0x02; // Mouse 2 (right button)
    const int VK_MOUSE5 = 0x06;  // Mouse 5
    const int VK_MOUSE1 = 0x01;  // Mouse 1

    public static void Main(string[] args)
    {
        // Initialize ImGui renderer
        OverlayRenderer renderer = new OverlayRenderer();
        renderer.Start().Wait();

        Swed window = new Swed("ac_client");

        // Offsets
        int localPlayerOffset = 0x17E0A8;
        int entityListOffset = 0x18AC04;
        int playerCountOffset = 0x18AC0C;

        while (true)
        {
            // Reinitialize module base on every loop
            IntPtr modulebase = window.GetModuleBase("ac_client.exe");

            // Re-read local player and entity list
            IntPtr localPlayerAddress = window.ReadPointer(modulebase, localPlayerOffset);
            IntPtr entityListAddress = window.ReadPointer(modulebase, entityListOffset);
            int playerCount = window.ReadInt(modulebase + playerCountOffset);

            if (localPlayerAddress == IntPtr.Zero || entityListAddress == IntPtr.Zero || playerCount == 0)
            {
                Console.WriteLine("No player in entity list, are you in game? Retrying...");
                Thread.Sleep(500); // Add a small delay to avoid fast-looping
                continue; // Skip this loop iteration if data is invalid
            }

            // Read local player information
            Vector3 localHeadPosition = window.ReadVec(localPlayerAddress + 0x4); // Head position offset
            float localYaw = window.ReadFloat(localPlayerAddress + 0x34); // Yaw
            float localPitch = window.ReadFloat(localPlayerAddress + 0x38); // Pitch
            int localPlayerTeam = window.ReadInt(localPlayerAddress + 0x30C); // Team ID

            // Variables for finding the closest enemy
            Vector3 closestHeadPosition = Vector3.Zero;
            float closestAngleDifference = float.MaxValue;

            // Loop through entities to find enemies
            for (int i = 0; i < playerCount; i++)
            {
                IntPtr entityPointer = window.ReadPointer(entityListAddress, i * 0x4); // Entities are 4 bytes apart
                if (entityPointer == IntPtr.Zero || entityPointer == localPlayerAddress) continue;

                // Check if entity is alive and on the opposite team
                bool isEnemyAlive = window.ReadInt(entityPointer + 0x318) == 0; // 0 = alive
                int enemyTeam = window.ReadInt(entityPointer + 0x30C); // Team offset
                if (!isEnemyAlive || enemyTeam == localPlayerTeam) continue; // Skip dead or teammate

                // Read enemy head position
                Vector3 enemyHeadPosition = window.ReadVec(entityPointer + 0x4); // Head position

                // Calculate the direction vector from local player to enemy
                Vector3 aimDirection = enemyHeadPosition - localHeadPosition;

                // Calculate yaw and pitch to target
                float targetYaw = (float)(Math.Atan2(aimDirection.Y, aimDirection.X) * (180.0 / Math.PI));
                float horizontalDistance = MathF.Sqrt(aimDirection.X * aimDirection.X + aimDirection.Y * aimDirection.Y);
                float targetPitch = (float)(Math.Atan2(aimDirection.Z, horizontalDistance) * (180.0 / Math.PI));

                // Adjust yaw to 270 degree base and normalize
                float adjustedYaw = targetYaw - 270f;
                if (adjustedYaw < -180f) adjustedYaw += 360f;
                if (adjustedYaw > 180f) adjustedYaw -= 360f;

                // Calculate the difference in angles
                float yawDifference = ClampAngle(adjustedYaw - localYaw);
                float pitchDifference = ClampAngle(targetPitch - localPitch);

                // Total angle difference
                float totalAngleDifference = MathF.Abs(yawDifference) + MathF.Abs(pitchDifference);

                // Find the enemy closest to the crosshair
                if (totalAngleDifference < closestAngleDifference)
                {
                    closestAngleDifference = totalAngleDifference;
                    closestHeadPosition = enemyHeadPosition;
                }
            }

            // Aimbot: Adjust the view angles when Mouse 1 is pressed
            if ((GetAsyncKeyState(VK_RBUTTON) & 0x8000) != 0 && closestAngleDifference != float.MaxValue && renderer.Aimbot)
            {
                // Aim at the closest enemy
                Vector3 aimDirection = closestHeadPosition - localHeadPosition;

                float targetYaw = (float)(Math.Atan2(aimDirection.Y, aimDirection.X) * (180.0 / Math.PI));
                float horizontalDistance = MathF.Sqrt(aimDirection.X * aimDirection.X + aimDirection.Y * aimDirection.Y);
                float targetPitch = (float)(Math.Atan2(aimDirection.Z, horizontalDistance) * (180.0 / Math.PI));

                float adjustedYaw = targetYaw - 270f;
                float adjustedPitch = targetPitch + 0.5f;
                if (adjustedYaw < -180f) adjustedYaw += 360f;
                if (adjustedYaw > 180f) adjustedYaw -= 360f;

                float deltaYaw = ClampAngle(adjustedYaw - localYaw);
                float deltaPitch = ClampAngle(adjustedPitch - localPitch);

                // Write new yaw and pitch values
                window.WriteFloat(localPlayerAddress + 0x34, localYaw + deltaYaw);
                window.WriteFloat(localPlayerAddress + 0x38, localPitch + deltaPitch);

            }

            // Teleportation: Teleport next to the enemy when Mouse 5 is pressed
            if ((GetAsyncKeyState(VK_MOUSE5) & 0x8000) != 0 && closestAngleDifference != float.MaxValue && renderer.Teleport)
            {
                Vector3 aimDirection = Vector3.Normalize(closestHeadPosition - localHeadPosition);
                float teleportDistance = 0.1f; // Adjust this value to determine how far to teleport
                Vector3 teleportPosition = closestHeadPosition - (aimDirection * teleportDistance);

                // Write the teleport position
                window.WriteVec(localPlayerAddress + 0x4, teleportPosition);
            }

            // Render ImGui options (Godmode, Infinite Ammo, etc.)
            if (renderer.Godmode)
            {
                IntPtr healthAddress = modulebase + 0x1C223;
                window.WriteBytes(healthAddress, "90 90 90"); // NOP instruction to avoid damage
            }

            if (renderer.InfiniteAmmo)
            {
                IntPtr ammoAddress = modulebase + 0xC73EF;
                window.WriteBytes(ammoAddress, "90 90"); // NOP instruction to freeze ammo count
            }

            if (renderer.bullettp)
            {
                // Check if Mouse 1 is pressed and there's a valid closest enemy
                if ((GetAsyncKeyState(VK_MOUSE1) & 0x8000) != 0 && closestAngleDifference != float.MaxValue)
                {
                    // Check if silent aim is not enabled
                    if (!renderer.silentaim)
                    {
                        // Ensure that a valid closest head position is set
                        if (closestHeadPosition != Vector3.Zero) // Check if we have a valid closest enemy
                        {
                            Vector3 aimDirection = closestHeadPosition - localHeadPosition;

                            float targetYaw = (float)(Math.Atan2(aimDirection.Y, aimDirection.X) * (180.0 / Math.PI));
                            float horizontalDistance = MathF.Sqrt(aimDirection.X * aimDirection.X + aimDirection.Y * aimDirection.Y);
                            float targetPitch = (float)(Math.Atan2(aimDirection.Z, horizontalDistance) * (180.0 / Math.PI));

                            float adjustedYaw = targetYaw - 270f;
                            float adjustedPitch = targetPitch + 0.7f;
                            if (adjustedYaw < -180f) adjustedYaw += 360f;
                            if (adjustedYaw > 180f) adjustedYaw -= 360f;

                            float deltaYaw = ClampAngle(adjustedYaw - localYaw);
                            float deltaPitch = ClampAngle(adjustedPitch - localPitch);

                            // Write new yaw and pitch values
                            window.WriteFloat(localPlayerAddress + 0x34, localYaw + deltaYaw);
                            window.WriteFloat(localPlayerAddress + 0x38, localPitch + deltaPitch);

                            // Calculate teleport position
                            Vector3 aimDirection2 = Vector3.Normalize(closestHeadPosition - localHeadPosition);
                            float teleportDistance = 0.1f; // Adjust this value to determine how far to teleport
                            Vector3 teleportPosition = closestHeadPosition - (aimDirection2 * teleportDistance);

                            // Write the teleport position
                            window.WriteVec(localPlayerAddress + 0x4, teleportPosition); // Assuming head position offset is 0x4

                            // Introduce a delay
                            long targetDelay = 3; // Target delay in microseconds (0.3 ms, avoids shooting walls)
                            Stopwatch stopwatch = new Stopwatch();
                            stopwatch.Start();
                            while (stopwatch.ElapsedTicks * 1000 / Stopwatch.Frequency < targetDelay)
                            {
                                // Busy-wait loop for 0.5ms
                            }
                            stopwatch.Stop();
                        }
                    }
                    else // Handle the silent aim case
                    {
                        // Ensure that a valid closest head position is set
                        if (closestHeadPosition != Vector3.Zero) // Check if we have a valid closest enemy
                        {
                            Vector3 aimDirection2 = Vector3.Normalize(closestHeadPosition - localHeadPosition);
                            float teleportDistance = 0.1f; // Adjust this value to determine how far to teleport
                            Vector3 teleportPosition = closestHeadPosition - (aimDirection2 * teleportDistance);

                            // Write the teleport position
                            window.WriteVec(localPlayerAddress + 0x4, teleportPosition);

                            // Introduce a delay
                            long targetDelay = 3; // Target delay in microseconds (0.3 ms, avoids shooting walls)
                            Stopwatch stopwatch = new Stopwatch();
                            stopwatch.Start();
                            while (stopwatch.ElapsedTicks * 1000 / Stopwatch.Frequency < targetDelay)
                            {
                                // Busy-wait loop for 0.5ms
                            }
                            stopwatch.Stop();
                        }
                    }
                }
            }
        }
    }

    // Clamp angles between -180 and 180 degrees
    static float ClampAngle(float angle)
    {
        while (angle < -180f) angle += 360f;
        while (angle > 180f) angle -= 360f;
        return angle;
    }
}