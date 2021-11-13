using System;
using System.Reflection;

using BepInEx.Logging;

using CalApi.API.Cat;

using Cat;

using HarmonyLib;

using Mono.Cecil.Cil;

using MonoMod.Cil;

using PipeSystem;

using UnityEngine;

namespace CatsAreOnline;

public class CapturedData {
    public CatPartManager? catPartManager { get; private set; }
    public CatControls? catControls { get; private set; }
    public Sprite? catSprite { get; private set; }
    public Color catColor { get; private set; }
    public Color catPipeColor { get; private set; }
    public State catState { get; private set; }
    public float catScale { get; private set; }

    public bool inIce { get; private set; }
    public IceBlock? iceBlock { get; private set; }
    public Sprite? iceSprite { get; private set; }
    public Color iceColor { get; private set; }
    public float iceRotation { get; private set; }

    public Transform? companionTransform { get; private set; }
    public Sprite? companionSprite { get; private set; }
    public Color companionColor { get; private set; }

    public bool inJunction { get; private set; }
    public Vector2 junctionPosition { get; private set; }

    public CapturedData(ManualLogSource logger, Client client) {
        logger.LogInfo("Initializing data capturing");

        catState = Cat.State.Normal;
        catScale = catState.GetScale();

        InitializePartManagerUpdates();
        InitializeControlsUpdates(client);
        InitializeColorUpdates();
        InitializeIceUpdates();
        InitializeIceRotationUpdates();
        InitializeJunctionUpdates();
    }

    private void InitializePartManagerUpdates() {
        FieldInfo noMetaballsPartTexture = AccessTools.Field(typeof(Cat.CatPartManager), "noMetaballsPartTexture");
        On.Cat.CatPartManager.Awake += (orig, self) => {
            orig(self);
            if(!self.GetComponent<PlayerActor>()) return;

            catSprite = (Sprite)noMetaballsPartTexture!.GetValue(self);
            catPartManager = self;
        };
    }

    private void InitializeControlsUpdates(Client client) => On.Cat.CatControls.Awake += (orig, self) => {
        orig(self);
        if(!self.GetComponent<PlayerActor>()) return;

        FieldInfo normalStateConfiguration = AccessTools.Field(typeof(CatControls), "normalStateConfiguration");
        FieldInfo stateConfigurationColor = AccessTools.Field(normalStateConfiguration.FieldType, "color");
        catColor = (Color)stateConfigurationColor.GetValue(normalStateConfiguration.GetValue(self));

        GameObject catIcePrefab =
            (GameObject)AccessTools.Field(typeof(Cat.CatControls), "catIcePrefab").GetValue(self);
        SpriteRenderer catIceMainRenderer =
            (SpriteRenderer)AccessTools.Field(typeof(IceBlock), "mainSprite")
                .GetValue(catIcePrefab.GetComponent<IceBlock>());

        iceSprite = catIceMainRenderer.sprite;
        iceColor = catIceMainRenderer.color;
        catControls = self;

        SubscribeToStateUpdates(self);

        SubscribeToCompanionUpdates(client, self);
    };

    private void SubscribeToStateUpdates(CatControls self) => self.StateSwitchAction += state => {
        catState = (Cat.State)state;
        catScale = catState.GetScale();
    };

    private void SubscribeToCompanionUpdates(Client client, CatControls self) => self.CompanionToggeledAction += enabled => {
        if(enabled) {
            Companion companion =
                (Companion)AccessTools.Field(typeof(Cat.CatControls), "companion").GetValue(self);
            companionTransform = companion.transform;
            SpriteRenderer renderer = companionTransform.Find("Companion Sprite")
                .GetComponent<SpriteRenderer>();
            companionSprite = renderer.sprite;
            companionColor = renderer.color;

            client.AddCompanion();
        }
        else {
            companionTransform = null;
            client.RemoveCompanion();
        }
    };

    private void InitializeColorUpdates() {
        void ChangedColor(Cat.CatControls self, Color newColor) {
            if(!self.GetComponent<PlayerActor>()) return;
            catColor = newColor;
        }

        // MM HookGen can't hook properly cuz of the arg being an internal struct so we do il manually
        IL.Cat.CatControls.ApplyConfiguration += il => {
            ILCursor cursor = new(il);
            cursor.GotoNext(code => code.MatchRet());
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<Cat.CatControls, Color>>(CatControlsExtensions.GetCurrentConfigurationColor);
            cursor.EmitDelegate<Action<Cat.CatControls, Color>>(ChangedColor);
        };
        On.Cat.CatControls.ApplyColor += (orig, self, color, featureColor) => {
            orig(self, color, featureColor);
            ChangedColor(self, color);
        };

        On.PipeSystem.PipeCatRepresentation.LateUpdate += (orig, self) => {
            orig(self);
            foreach(Material material in (Material[])AccessTools.Field(typeof(PipeCatRepresentation), "materials")
                        .GetValue(self)) {
                if(!material) continue;
                catPipeColor = material.color;
                break;
            }
        };
    }

    private void InitializeIceUpdates() {
        On.Cat.CatControls.ActivateIce += (orig, self) => {
            orig(self);
            inIce = self.IsCatIceActive();
            if(!inIce) return;
            iceBlock = self.GetActiveCatIce().GetComponent<IceBlock>();
        };

        On.Cat.CatControls.DeactivateIce += (orig, self, destroyed) => {
            orig(self, destroyed);
            inIce = false;
            iceBlock = null;
        };
    }

    private void InitializeIceRotationUpdates() => On.Cat.CatControls.FixedUpdate += (orig, self) => {
        orig(self);
        if(self != catControls || !self.IsCatIceActive()) return;
        iceRotation = self.GetActiveCatIce().transform.eulerAngles.z;
    };

    private void InitializeJunctionUpdates() {
        FieldInfo controller = AccessTools.Field(typeof(PipeObject), "controller");

        On.PipeSystem.PipeJunction.EnterJunction += (orig, self, pipeObject) => {
            orig(self, pipeObject);
            if(controller.GetValue(pipeObject) == null) return;
            junctionPosition = self.transform.position;
            inJunction = true;
        };

        On.PipeSystem.PipeJunction.ExitJunction += (orig, self, pipeObject, point) => {
            orig(self, pipeObject, point);
            if(controller.GetValue(pipeObject) == null) return;
            inJunction = false;
        };

        On.Cat.CatPartManager.SpawnCatCoroutine += (orig, self, position) => {
            inJunction = false;
            return orig(self, position);
        };
    }
}
