using BepInEx;
using BepInEx.Configuration;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RiskOfOptions;
using RiskOfOptions.OptionConfigs;
using RiskOfOptions.Options;
using RoR2;
using RoR2.Projectile;
using System.IO;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace BetterShurikenHoming
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency("com.rune580.riskofoptions")]
    public class BetterShurikenHoming : BaseUnityPlugin
    {
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "SSM24";
        public const string PluginName = "BetterShurikenHoming";
        public const string PluginVersion = "1.0.0";

        public static ConfigEntry<bool> IgnoreEnemyDistance;
        public static ConfigEntry<float> MaxAngle;
        public static ConfigEntry<float> MaxDistance;

        private const float default_MaxAngle = 4f;
        private const float default_MaxDistance = 150f;

        public void Awake()
        {
            Log.Init(Logger);

            IgnoreEnemyDistance = Config.Bind("Config", "Always Prioritize Aim", true,
                "Always target enemy nearest to the crosshair, instead of sometimes prioritizing enemies closer to the player.\n\nVanilla default is false");
            MaxAngle = Config.Bind("Config", "Max Angle", default_MaxAngle,
                "The width of the targeting cone in degrees. Higher values tend to make shurikens chase enemies they can't hit.\n\nVanilla default is 90");
            MaxDistance = Config.Bind("Config", "Max Distance", default_MaxDistance,
                "The maximum distance a shuriken can detect enemies, in meters.\n\nVanilla default is 100");

            ModSettingsManager.AddOption(new CheckBoxOption(IgnoreEnemyDistance));
            ModSettingsManager.AddOption(new SliderOption(MaxAngle, new SliderConfig
            {
                min = 0f,
                max = 180f,
                formatString = "{0:F1}°",
                description = MaxAngle.Description.Description + $"\nRecommended value is {default_MaxAngle}"
            }));
            ModSettingsManager.AddOption(new SliderOption(MaxDistance, new SliderConfig
            {
                min = 0f,
                max = 500f,
                formatString = "{0:F0}m",
                description = MaxDistance.Description.Description + $"\nRecommended value is {default_MaxDistance}"
            }));

            // create icon from file
            // mostly taken from https://github.com/Vl4dimyr/CaptainShotgunModes/blob/fdf828e/RiskOfOptionsMod.cs#L36-L48
            // i have NO clue what this code is doing but it seems to work so... cool?
            try
            {
                using Stream stream = File.OpenRead(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Info.Location), "icon.png"));
                Texture2D texture = new Texture2D(0, 0);
                byte[] imgData = new byte[stream.Length];

                stream.Read(imgData, 0, (int)stream.Length);

                if (ImageConversion.LoadImage(texture, imgData))
                {
                    ModSettingsManager.SetModIcon(
                        Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0, 0))
                    );
                }
            }
            catch (FileNotFoundException)
            {
            }

            IL.RoR2.Projectile.ProjectileDirectionalTargetFinder.SearchForTarget += IL_ProjectileDirectionalTargetFinder_SearchForTarget;
        }

        private void IL_ProjectileDirectionalTargetFinder_SearchForTarget(ILContext il)
        {
            ILCursor cursor = new(il);
            // right before the target search is done
            if (cursor.TryGotoNext(MoveType.After, instr => instr.MatchCallvirt<BullseyeSearch>("set_maxAngleFilter")))
            {
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate(ModifyBullseyeSearch);
            }
            else
            {
                Log.Error("could not hook ProjectileDirectionalTargetFinder.SearchForTarget");
            }
        }

        private static void ModifyBullseyeSearch(ProjectileDirectionalTargetFinder self)
        {
            if (self.gameObject.name.Contains("ShurikenProjectile"))
            {
                if (IgnoreEnemyDistance.Value)
                {
                    self.bullseyeSearch.sortMode = BullseyeSearch.SortMode.Angle;
                }
                self.bullseyeSearch.maxAngleFilter = MaxAngle.Value;
                self.bullseyeSearch.maxDistanceFilter = MaxDistance.Value;
            }
        }
    }
}
