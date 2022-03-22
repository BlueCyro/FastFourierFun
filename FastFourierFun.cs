using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using FrooxEngine;
using FrooxEngine.LogiX;
using BaseX;
using CodeX;
using NeosModLoader;
using HarmonyLib;
using CSCore;
using CSCore.DSP;

#pragma warning disable CS8603 // Possible null reference return.

namespace FastFourierFun;
public class Fourier : NeosMod
{
    public override string Author => "Cyro";
    public override string Name => "Fast Fourier Fun";
    public override string Version => "1.1.0";
	static FftSize FFTSize = FftSize.Fft2048;

    public override void OnEngineInit()
    {
        Harmony harmony = new Harmony("net.Cyro.FastFourierFun");
        harmony.PatchAll();
    }

    [HarmonyPatch(typeof(LogixTip), "GenerateMenuItems")]
    public static class FourierPatch
    {
		private static StaticAudioClip GetHeldAudioClip(LogixTip __instance, out string slotname)
		{
			slotname = "";
			var source = __instance.ActiveTool.Grabber.HolderSlot.GetComponentInChildren<IReferenceSource>();
			//UniLog.Log("Source: " + source);

			if (source == null)
				return null;
			
			if (source.UntypedReference != null)
			{
				slotname = source.Slot.Name;
				//UniLog.Log("UntypedReference: " + source.UntypedReference);
				if (source.UntypedReference is StaticAudioClip)
				{
					UniLog.Log("UntypedReference is StaticAudioClip");
					return source.UntypedReference as StaticAudioClip;
				}
			}
			return null;
		}

		private static TextRenderer SetupText(Slot slot, string text)
		{
			Slot Identifier = slot.AddSlot("Text");
			Identifier.LocalPosition = new float3(0f, 0f, -0.52f);
			var TextRenderer = Identifier.AttachComponent<TextRenderer>();
			TextRenderer.Text.Value = text;
			TextRenderer.Size.Value = 1.8f;
			TextRenderer.Bounded.Value = true;
			TextRenderer.VerticalAutoSize.Value = true;
			
			var TextMat = slot.AttachComponent<TextUnlitMaterial>();
			TextMat.OutlineColor.Value = color.Black;
			TextMat.OutlineThickness.Value = 0.1f;
			TextMat.FaceDilate.Value = 0.11f;

			TextRenderer.Material.Target = TextMat;

			return TextRenderer;
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

        private static async Task GetAudioClipFFT(IButton button, ButtonEventData eventData, LogixTip __instance, StaticAudioClip audioClip, string slotname, int SliceLength = 2048, int ReadFrequency = 1, int SliceTo = 0)
		{
			TextRenderer? Text = null;
			__instance.RunSynchronously(() => 
			{
				Slot ProgressText = __instance.LocalUserSpace.AddSlot("Progress");
				ProgressText.PersistentSelf = false;
				Text = ProgressText.AttachComponent<TextRenderer>();
				var positioner = ProgressText.AttachComponent<LookAtUser>();
				var destroyer = ProgressText.AttachComponent<DestroyOnUserLeave>();
				destroyer.TargetUser.Target = __instance.LocalUser;

				ProgressText.PositionInFrontOfUser(new float3(0f, 0f, -1f), null, 0.7f);
				ProgressText.GlobalScale = new float3(0.35f, 0.35f, 0.35f) * __instance.LocalUserRoot.GlobalScale;
				positioner.TargetAtLocalUser.Value = true;
				positioner.Invert.Value = true;
			});
			
			while (Text == null)
			{
				await Task.Delay(100);
			}

			if (audioClip == null)
				return;

			UniLog.Log("Audioclip found!");
			AudioClip asset = audioClip.Asset;
			AnimX anim = await Task.Run(() => asset.GetFFTAnimation(Text.Text, SliceLength, ReadFrequency, SliceTo));
			LocalDB dB = __instance.World.Engine.LocalDB;

			string tempPath = dB.GetTempFilePath("animx");
			UniLog.Log("Waiting for asset to be loaded...");
			__instance.RunSynchronously(() => Text.Text.Value = "Waiting for asset to be loaded...");
			anim.SaveToFile(tempPath, AnimX.Encoding.LZMA);
			Uri animUri = await dB.ImportLocalAssetAsync(tempPath, LocalDB.ImportLocation.Move, null);

			__instance.RunSynchronously(() =>
			{
				Text.Slot.Destroy();
				Slot FFTStore = SetupVisual();
				Slot AssetStore = FFTStore.AddSlot("Clip Storage");

				SetupText(AssetStore, slotname);

				StaticAnimationProvider animationProvider = AssetStore.AttachComponent<StaticAnimationProvider>();
				animationProvider.URL.Value = animUri;
				AssetStore.CreateReferenceVariable<IAssetProvider<FrooxEngine.Animation>>("AnimationProvider", animationProvider);
				AssetStore.CreateReferenceVariable<IAssetProvider<AudioClip>>("AudioClip", AssetStore.DuplicateComponent<StaticAudioClip>(audioClip, false));
				AssetStore.CreateVariable<int>("FFTBinSize", SliceLength);
				AssetStore.CreateVariable<int>("FFTReadFrequency", ReadFrequency);
				AssetStore.CreateVariable<int>("FFTSlicedTo", SliceTo);
				AssetStore.CreateVariable<int>("FFTTotalFrames", audioClip.Asset.Data.SampleCount / audioClip.Asset.Data.ChannelCount / (SliceLength * ReadFrequency));
				animationProvider.ForceLoad();

				FFTStore.GlobalScale = new float3(0.1f, 0.1f, 0.1f) * __instance.LocalUserRoot.GlobalScale;
				FFTStore.PositionInFrontOfUser(new float3(0f, 0f, -1f), null, 0.7f);
			});
		}

		private static void PressButton(IButton button, ButtonEventData eventData, LogixTip __instance, ContextMenu menu)
		{
			__instance.StartTask((Func<Task>) (async () => {
				CommonTool summon = eventData.source.Slot.FindCommonTool();
				Slot point = summon.PointReference;
				ContextMenu menu = await __instance.LocalUser.OpenContextMenu(summon, point);
				Slot root = __instance.LocalUserRoot.Slot;
				DynamicVariableSpace space = __instance.LocalUserRoot.Slot.FindSpace("User");
				StaticAudioClip audioClip = GetHeldAudioClip(__instance, out string slotname);
				if (space == null)
					return;

				if (!space.TryReadValue<FftSize>("FftSize", out FftSize FFTVal))
					root.CreateVariable<FftSize>("User/FftSize", FFTVal = FftSize.Fft2048);
				
				if (!space.TryReadValue<int>("ReadFrequency", out int ReadFrequency))
					root.CreateVariable<int>("User/ReadFrequency", ReadFrequency = 1);
				
				if (!space.TryReadValue<int>("SliceTo", out int SliceTo))
					root.CreateVariable<int>("User/SliceTo", SliceTo = 256);

				var descripts = new List<OptionDescription<FftSize>>();
				var readMults = new List<OptionDescription<int>>();
				var sliceTos = new List<OptionDescription<int>>();

				int counter = 0;
				color green = color.Green;
				color red = color.Red;

				foreach(int i in Enum.GetValues(typeof(FftSize)))
				{
					descripts.Add(new OptionDescription<FftSize>((FftSize)i, ((FftSize)i).ToString(), MathX.Lerp(red, green, (float)counter / (float)Enum.GetValues(typeof(FftSize)).Length), NeosAssets.Common.Icons.Audio, null, null));
					sliceTos.Add(new OptionDescription<int>(i, "Slice to: " + i.ToString(), MathX.Lerp(red, green, (float)counter / (float)Enum.GetValues(typeof(FftSize)).Length), NeosAssets.Common.Icons.Cutlery, null, null));
					counter++;
				}
				
				for (int i = 0; i <= 8; i++)
				{
					// This is probably not efficient, but it's hilarious.
					int index = Enumerable.Repeat(2, i).Aggregate(1, (a,b) => a * b);
					readMults.Add(new OptionDescription<int>(index, "Read Frequency: " + index.ToString(), MathX.Lerp(red, green, (float)i / 16f), NeosAssets.Common.Icons.Power, null, null));
				}

				var FftItem = menu.AddItem("HOW", NeosAssets.Common.Icons.Audio, color.White, null);
				var FftVar = FftItem.Slot.AttachComponent<DynamicValueVariable<FftSize>>();
				FftVar.VariableName.Value = "User/FftSize";
				FftItem.SetupValueCycle(FftVar.Value, descripts);
				
				var ReadMultItem = menu.AddItem("WHAT", NeosAssets.Common.Icons.Power, color.White, null);
				var ReadMultVar = ReadMultItem.Slot.AttachComponent<DynamicValueVariable<int>>();
				ReadMultVar.VariableName.Value = "User/ReadFrequency";
				ReadMultItem.SetupValueCycle(ReadMultVar.Value, readMults);
				
				var SliceItem = menu.AddItem("WHERE", NeosAssets.Common.Icons.Audio, color.White, null);
				var SliceVar = SliceItem.Slot.AttachComponent<DynamicValueVariable<int>>();
				SliceVar.VariableName.Value = "User/SliceTo";
				SliceItem.SetupValueCycle(SliceVar.Value, sliceTos);

				var menuItem = menu.AddItem("Get FFT", NeosAssets.Common.Icons.Broadcast, new color(0f, 0.5f, 1f));
				menuItem.Button.LocalPressed += (IButton b, ButtonEventData d) => GetAudioClipFFT(button, eventData, __instance, audioClip, slotname, (int)FftVar.Value.Value, ReadMultVar.Value.Value, SliceVar.Value.Value).ConfigureAwait(false);
			}));
		}
        public static void Postfix(CommonTool tool, ContextMenu menu, LogixTip __instance)
        {
			if (GetHeldAudioClip(__instance, out _) != null)
			{
            	var menuItem = menu.AddItem("Get AudioClip FFT", NeosAssets.Common.Icons.Audio, new color(1f, 1f, 0f), null);

				menuItem.Button.LocalPressed += (IButton b, ButtonEventData d) => PressButton(b, d, __instance, menu);

			}
		}
    }
}
