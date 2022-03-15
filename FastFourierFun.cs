using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Reflection;
using FrooxEngine;
using FrooxEngine.LogiX;
using BaseX;
using CodeX;
using NeosModLoader;
using HarmonyLib;
using CSCore;
using CSCore.DSP;

#pragma warning disable CS8603 // Possible null reference return.

namespace YourNamespaceHere;
public class ModClass : NeosMod
{
    public override string Author => "Cyro";
    public override string Name => "Fast Fourier Fun";
    public override string Version => "1.0.0";

    public override void OnEngineInit()
    {
        Harmony harmony = new Harmony("net.Cyro.FastFourierFun");
        harmony.PatchAll();
    }

    [HarmonyPatch(typeof(LogixTip), "GenerateMenuItems")]
    public static class FourierPatch
    {
		private static StaticAudioClip GetHeldAudioClip(LogixTip __instance)
		{
			var source = __instance.ActiveTool.Grabber.HolderSlot.GetComponentInChildren<IReferenceSource>();
			UniLog.Log("Source: " + source);
			if (source == null)
				return null;
			
			if (source.UntypedReference != null)
			{
				UniLog.Log("UntypedReference: " + source.UntypedReference);
				if (source.UntypedReference is StaticAudioClip)
				{
					UniLog.Log("UntypedReference is StaticAudioClip");
					return source.UntypedReference as StaticAudioClip;
				}
			}
			return null;
		}

		private static Slot SetupVisual()
		{
			UniLog.Log("Setting Up Visual");
			World world = Engine.Current.WorldManager.FocusedWorld;
			UniLog.Log("World: " + world);
			UniLog.Log("Local User: " + world.LocalUser);
			UniLog.Log("Local User Space: " + world.LocalUser.LocalUserSpace);
			Slot FFTStore = world.LocalUser.LocalUserSpace.AddSlot("FFT Data");
			
			UniLog.Log("FFTStore: " + FFTStore);

			BoxMesh mesh = FFTStore.AttachComponent<BoxMesh>();
			MeshRenderer renderer = FFTStore.AttachComponent<MeshRenderer>();
			PBS_Specular spec = FFTStore.AttachComponent<PBS_Specular>();
			BoxCollider collider = FFTStore.AttachComponent<BoxCollider>();
			Grabbable grabbable = FFTStore.AttachComponent<Grabbable>();

			grabbable.Scalable.Value = true;
			renderer.Mesh.Target = mesh;
			renderer.Material.Target = spec;

			Snapper FFTSnapper = FFTStore.AttachComponent<Snapper>();
			FFTSnapper.Keywords.Add().Value = "FFTSnapper";

			return FFTStore;
		}

        private static async Task GetAudioClipFFT(IButton button, ButtonEventData eventData, LogixTip __instance, Sync<string> Progress, int SliceLength = 2048, int ReadMultiplier = 1, int SliceTo = 0)
		{
			StaticAudioClip audioClip = GetHeldAudioClip(__instance);

			if (audioClip == null)
				return;

			UniLog.Log("Audioclip contents: " + audioClip.ToString());
			AudioClip asset = audioClip.Asset;
			AnimX anim = await Task.Run(() => asset.GetFFTAnimation(Progress, SliceLength, ReadMultiplier, SliceTo));
			
			LocalDB dB = __instance.World.Engine.LocalDB;

			string tempPath = dB.GetTempFilePath("animx");
			UniLog.Log("Waiting for asset to be loaded...");
			__instance.RunSynchronously(() => Progress.Value = "Waiting for asset to be loaded...");
			anim.SaveToFile(tempPath, AnimX.Encoding.LZMA);
			Uri animUri = await dB.ImportLocalAssetAsync(tempPath, LocalDB.ImportLocation.Move, null);

		

			__instance.RunSynchronously(() =>
			{
				Progress.Component.Destroy();
				Slot FFTStore = SetupVisual();
				Slot AssetStore = FFTStore.AddSlot("Clip Storage");

				var providerVar = AssetStore.AttachComponent<DynamicReferenceVariable<StaticAnimationProvider>>();
				providerVar.VariableName.Value = "AnimationProvider";

				var audioClipVar = AssetStore.AttachComponent<DynamicReferenceVariable<IAssetProvider<AudioClip>>>();
				audioClipVar.VariableName.Value = "AudioClip";
				audioClipVar.Reference.Target = AssetStore.DuplicateComponent<StaticAudioClip>(audioClip, false);

				StaticAnimationProvider animationProvider = AssetStore.AttachComponent<StaticAnimationProvider>();
				animationProvider.URL.Value = animUri;
				providerVar.Reference.Target = animationProvider;

				var AssLoader = AssetStore.AttachComponent<AssetLoader<FrooxEngine.Animation>>();
				AssLoader.Asset.Target = animationProvider;

				FFTStore.GlobalScale = new float3(0.186f, 0.186f, 0.186f) * __instance.LocalUserRoot.GlobalScale;
				FFTStore.PositionInFrontOfUser(new float3(0f, 0f, -1f), null, 0.7f);
			});

		}

		private static void PressButton(IButton button, ButtonEventData eventData, LogixTip __instance, ContextMenu menu)
		{
			Slot ProgressText = __instance.LocalUserSpace.AddSlot("Progress");
			ProgressText.PersistentSelf = false;
			var Text = ProgressText.AttachComponent<TextRenderer>();
			var positioner = ProgressText.AttachComponent<LookAtUser>();

			ProgressText.PositionInFrontOfUser(new float3(0f, 0f, -1f), null, 0.7f);
			positioner.TargetAtLocalUser.Value = true;
			positioner.Invert.Value = true;

			__instance.StartTask((Func<Task>) (async () => {
				var summon = eventData.source.Slot.FindCommonTool();
				var point = summon.PointReference;
				var menu = await __instance.LocalUser.OpenContextMenu(summon, point);

				foreach(int i in Enum.GetValues(typeof(FftSize)))
				{
					var item = menu.AddItem(i.ToString(), NeosAssets.Common.Icons.Audio, new color(1f, 0f, 0f), null);
					item.Button.LocalPressed += (IButton b, ButtonEventData d) => GetAudioClipFFT(b, d, __instance, Text.Text, i, 4, 256).ConfigureAwait(false);
				}
				

			}));
		}
        public static void Postfix(CommonTool tool, ContextMenu menu, LogixTip __instance)
        {
			if (GetHeldAudioClip(__instance) != null)
			{
            	var menuItem = menu.AddItem("Get AudioClip FFT", NeosAssets.Common.Icons.Audio, new color(1f, 1f, 0f), null);

				menuItem.Button.LocalPressed += (IButton b, ButtonEventData d) => PressButton(b, d, __instance, menu);

			}
		}
    }
}
