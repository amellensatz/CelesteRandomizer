﻿using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;

namespace Celeste.Mod.Randomizer {
    public class RandoModule : EverestModule {
        public static RandoModule Instance;

        public RandoSettings Settings;
        public const int MAX_SEED_DIGITS = 6;

        public RandoModule() {
            Instance = this;

            Settings = new RandoSettings {
                Seed = 0,
                Dashes = NumDashes.One,
            };
        }

        public override void Load() {
            Everest.Events.MainMenu.OnCreateButtons += CreateMainMenuButton;
            Everest.Events.Level.OnCreatePauseMenuButtons += ModifyLevelMenu;
            Everest.Events.Level.OnTransitionTo += ResetCoreMode;
            On.Celeste.OverworldLoader.ctor += EnterToRandoMenu;
            On.Celeste.Overworld.ctor += HideMaddy;
            On.Celeste.MapData.Load += DontLoadRandoMaps;
            On.Celeste.AreaData.Load += InitRandoData;
            On.Celeste.TextMenu.MoveSelection += DisableMenuMovement;
            On.Celeste.Cassette.CollectRoutine += NeverCollectCassettes;
            On.Celeste.AreaComplete.VersionNumberAndVariants += AreaCompleteDrawHash;
            //On.Celeste.AutoSplitterInfo.Update += wtf;
        }

        public override void LoadContent(bool firstLoad) {
        }


        public override void Unload() {
            Everest.Events.MainMenu.OnCreateButtons -= CreateMainMenuButton;
            Everest.Events.Level.OnCreatePauseMenuButtons -= ModifyLevelMenu;
            Everest.Events.Level.OnTransitionTo -= ResetCoreMode;
            On.Celeste.OverworldLoader.ctor -= EnterToRandoMenu;
            On.Celeste.Overworld.ctor -= HideMaddy;
            On.Celeste.MapData.Load -= DontLoadRandoMaps;
            On.Celeste.AreaData.Load -= InitRandoData;
            On.Celeste.TextMenu.MoveSelection -= DisableMenuMovement;
            On.Celeste.Cassette.CollectRoutine -= NeverCollectCassettes;
            On.Celeste.AreaComplete.VersionNumberAndVariants += AreaCompleteDrawHash;
            //On.Celeste.AutoSplitterInfo.Update -= wtf;
        }

        public void wtf(On.Celeste.AutoSplitterInfo.orig_Update orig, AutoSplitterInfo self) {
            orig(self);
        }

        public override void CreateModMenuSection(TextMenu menu, bool inGame, EventInstance snapshot) {
            base.CreateModMenuSection(menu, inGame, snapshot);
        }

        public bool InRandomizer {
            get {
                if (SaveData.Instance == null) {
                    return false;
                }
                if (SaveData.Instance.CurrentSession == null) {
                    return false;
                }
                if (SaveData.Instance.CurrentSession.Area == null) {
                    return false;
                }
                AreaData area = AreaData.Get(SaveData.Instance.CurrentSession.Area);
                return area.GetSID().StartsWith("randomizer/");
            }
        }

        public void CreateMainMenuButton(OuiMainMenu menu, System.Collections.Generic.List<MenuButton> buttons) {
            MainMenuSmallButton btn = new MainMenuSmallButton("MODOPTIONS_RANDOMIZER_TOPMENU", "menu/randomizer", menu, Vector2.Zero, Vector2.Zero, () => {
                Audio.Play(SFX.ui_main_button_select);
                Audio.Play(SFX.ui_main_whoosh_large_in);
                menu.Overworld.Goto<OuiRandoSettings>();
            });
            buttons.Insert(1, btn);
        }

        public void ModifyLevelMenu(Level level, TextMenu pausemenu, bool minimal) {
            if (this.InRandomizer) {
                foreach (var item in new System.Collections.Generic.List<TextMenu.Item>(pausemenu.GetItems())) {
                    if (item.GetType() == typeof(TextMenu.Button)) {
                        var btn = (TextMenu.Button)item;
                        if (btn.Label == Dialog.Clean("MENU_PAUSE_SAVEQUIT") || btn.Label == Dialog.Clean("MENU_PAUSE_RETURN")) {
                            pausemenu.Remove(item);
                        }
                        if (btn.Label == Dialog.Clean("MENU_PAUSE_RESTARTAREA")) {
                            btn.Label = Dialog.Clean("MENU_PAUSE_RESTARTRANDO");
                        }
                    }
                }

                int returnIdx = pausemenu.GetItems().Count;
                pausemenu.Add(new TextMenu.Button(Dialog.Clean("MENU_PAUSE_QUITRANDO")).Pressed(() => {
                    level.PauseMainMenuOpen = false;
                    pausemenu.RemoveSelf();

                    TextMenu menu = new TextMenu();
                    menu.AutoScroll = false;
                    menu.Position = new Vector2((float)Engine.Width / 2f, (float)((double)Engine.Height / 2.0 - 100.0));
                    menu.Add(new TextMenu.Header(Dialog.Clean("MENU_QUITRANDO_TITLE")));
                    menu.Add(new TextMenu.Button(Dialog.Clean("MENU_QUITRANDO_CONFIRM")).Pressed((Action)(() => {
                        Engine.TimeRate = 1f;
                        menu.Focused = false;
                        level.Session.InArea = false;
                        Audio.SetMusic((string)null, true, true);
                        Audio.BusStopAll("bus:/gameplay_sfx", true);
                        level.DoScreenWipe(false, (Action)(() => Engine.Scene = (Scene)new LevelExit(LevelExit.Mode.SaveAndQuit, level.Session, level.HiresSnow)), true);
                        foreach (LevelEndingHook component in level.Tracker.GetComponents<LevelEndingHook>()) {
                            if (component.OnEnd != null)
                                component.OnEnd();
                        }
                    })));
                    menu.Add(new TextMenu.Button(Dialog.Clean("MENU_QUITRANDO_CANCEL")).Pressed((Action)(() => menu.OnCancel())));
                    menu.OnPause = menu.OnESC = (Action)(() => {
                        menu.RemoveSelf();
                        level.Paused = false;
                        Engine.FreezeTimer = 0.15f;
                        Audio.Play("event:/ui/game/unpause");
                    });
                    menu.OnCancel = (Action)(() => {
                        Audio.Play("event:/ui/main/button_back");
                        menu.RemoveSelf();
                        level.Pause(returnIdx, minimal, false);
                    });
                    level.Add((Entity)menu);
                }));
            }
        }

        public void ResetCoreMode(Level level, LevelData next, Vector2 direction) {
            if (this.InRandomizer) {
                level.CoreMode = Session.CoreModes.None;
                level.Session.CoreMode = Session.CoreModes.None;
            }
        }

        public void EnterToRandoMenu(On.Celeste.OverworldLoader.orig_ctor orig, OverworldLoader self, Overworld.StartMode startMode, HiresSnow snow) {
            if ((startMode == Overworld.StartMode.MainMenu || startMode == Overworld.StartMode.AreaComplete) && this.InRandomizer) {
                startMode = (Overworld.StartMode)55;
            }
            orig(self, startMode, snow);
        }

        // This is a bit of a hack. is there a better way to control this?
        public void HideMaddy(On.Celeste.Overworld.orig_ctor orig, Overworld self, OverworldLoader loader) {
            orig(self, loader);
            if (this.InRandomizer) {
                self.Maddy.Hide();
            }
        }

        public void DontLoadRandoMaps(On.Celeste.MapData.orig_Load orig, MapData self) {
            if (self.Data?.GetSID()?.StartsWith("randomizer/") ?? false) {
                return;
            }
            orig(self);
        }

        public void InitRandoData(On.Celeste.AreaData.orig_Load orig) {
            orig();
            RandoLogic.ProcessAreas();
            Settings.SetNormalMaps();
        }

        public void DisableMenuMovement(On.Celeste.TextMenu.orig_MoveSelection orig, TextMenu self, int direction, bool wiggle = false) {
            if (self is DisablableTextMenu newself && newself.DisableMovement) {
                return;
            }
            orig(self, direction, wiggle);
        }

        public IEnumerator NeverCollectCassettes(On.Celeste.Cassette.orig_CollectRoutine orig, Cassette self, Player player) {
            var thing = orig(self, player);
            while (thing.MoveNext()) {  // why does it not let me use foreach?
                yield return thing.Current;
            }

            if (this.InRandomizer) {
                var level = self.Scene as Level;
                level.Session.Cassette = false;
            }
        }

        public void AreaCompleteDrawHash(On.Celeste.AreaComplete.orig_VersionNumberAndVariants orig, string version, float ease, float alpha) {
            if (this.InRandomizer) {
                SaveData.Instance.VariantMode = false;
            }
            orig(version, ease, alpha);

            if (this.InRandomizer) {
                var text = this.Settings.Seed.ToString(RandoModule.MAX_SEED_DIGITS);
                if (this.Settings.Rules != Ruleset.Custom) {
                    text += " " + this.Settings.Rules.ToString();
                }
                text += "\n#" + this.Settings.Hash.ToString();
                ActiveFont.DrawOutline(text, new Vector2(1820f + 300f * (1f - Ease.CubeOut(ease)), 925f), new Vector2(0.5f, 0f), Vector2.One * 0.5f, Color.White, 2f, Color.Black);
            }
        }
    }

    public class DisablableTextMenu : TextMenu {
        public bool DisableMovement;
    }
}
