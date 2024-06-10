﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.Localization;
using Terraria.Nitrate.Localization;
using Terraria.UI;
using Terraria.UI.Chat;

namespace Terraria.Nitrate.UI.States;

internal sealed class UILanguageSelectMenu(bool usedForFirstTimeSetup) : UIState
{
	private sealed class UIGameCultureListItem : UIElement
	{
		public GameCulture Culture { get; }
		
		private readonly UILanguageSelectMenu menu;
		
		public UIGameCultureListItem(GameCulture culture, UILanguageSelectMenu menu)
		{
			Culture = culture;
			this.menu = menu;
			OnLeftClick += SetLanguage;
		}
		
		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			base.DrawSelf(spriteBatch);
			
			var dimensions = GetDimensions();
			var maxWidth = dimensions.Width + 1f;
			var pos = dimensions.Position();
			var panelColor = new Color(63, 82, 151);
			var baseScale = new Vector2(0.8f);
			
			if (IsMouseHovering)
			{
				panelColor = panelColor.MultiplyRGBA(new Color(180, 180, 180));
			}
			
			var name = Culture.LanguageName;
			if (Culture.LanguageName != Culture.EnglishName)
			{
				name += " / " + Culture.EnglishName;
			}
			
			Utils.DrawSettingsPanel(spriteBatch, pos, maxWidth, panelColor);
			
			pos.X += 8f;
			pos.Y += 8f;
			
			ChatManager.DrawColorCodedStringWithShadow(spriteBatch, FontAssets.ItemStack.Value, name, pos, IsMouseHovering ? Color.Silver : Color.White, 0f, Vector2.Zero, baseScale, maxWidth);
			
			// TODO: Show progress.
			{
				// var progress = "??/?? (xxx%);
				// localization_keys_100 = Colors.RarityGreen;
				// localization_keys_000 = Colors.RarityRed;
				const string progress = "";
				var stringSize = ChatManager.GetStringSize(FontAssets.ItemStack.Value, progress, baseScale);
				pos = new Vector2(dimensions.X + dimensions.Width - stringSize.X - 10f, dimensions.Y + 8f);
				ChatManager.DrawColorCodedStringWithShadow(spriteBatch, FontAssets.ItemStack.Value, progress, pos, IsMouseHovering ? Color.Silver : Color.White, 0f, Vector2.Zero, baseScale, maxWidth);
			}
		}
		
		private void SetLanguage(UIMouseEvent e, UIElement el)
		{
			LanguageManager.Instance.SetLanguage(Culture);
			Main.SaveSettings();
			menu.Recalculate();
			Main.changeTheTitle = true;
			
			if (menu.usedForFirstTimeSetup)
			{
				Main.menuMode = 0;
			}
		}
	}
	
	private readonly bool usedForFirstTimeSetup = usedForFirstTimeSetup;
	
	public override void OnActivate()
	{
		base.OnActivate();
		
		var outerContainer = new UIElement();
		outerContainer.Width.Set(0f, 0.8f);
		outerContainer.MaxWidth.Set(600f, 0f);
		outerContainer.Top.Set(220f, 0f);
		outerContainer.Height.Set(-200f, 1f);
		outerContainer.HAlign = 0.5f;
		{
			var backPanel = new UIPanel();
			backPanel.Width.Set(0f, 1f);
			backPanel.Height.Set(-110f, 1f);
			backPanel.BackgroundColor = new Color(33, 43, 79) * 0.8f;
			{
				var languageList = new UIList();
				languageList.Width.Set(-25f, 1f);
				languageList.Height.Set(-10f, 1f);
				languageList.VAlign = 1f;
				languageList.ListPadding = 4f;
				backPanel.Append(languageList);
				
				var scrollbar = new UIScrollbar();
				scrollbar.SetView(100f, 1000f);
				scrollbar.Height.Set(0f, 1f);
				scrollbar.HAlign = 1f;
				scrollbar.VAlign = 1f;
				backPanel.Append(scrollbar);
				languageList.SetScrollbar(scrollbar);
				
				PopulateListWithLanguages(languageList, this);
			}
			
			outerContainer.Append(backPanel);
			
			var selectLanguageText = new UITextPanel<LocalizedText>(Lang.menu[102], 0.7f, large: true);
			selectLanguageText.Top.Set(-45f, 0f);
			selectLanguageText.HAlign = 0.5f;
			selectLanguageText.SetPadding(15f);
			selectLanguageText.BackgroundColor = new Color(73, 94, 171);
			outerContainer.Append(selectLanguageText);
			
			if (!usedForFirstTimeSetup)
			{
				var backButton = new UITextPanel<LocalizedText>(Language.GetText("UI.Back"), 0.7f, large: true);
				backButton.Width.Set(-10f, 0.5f);
				backButton.Height.Set(50f, 0f);
				backButton.VAlign = 1f;
				backButton.HAlign = 0.5f;
				backButton.Top.Set(-45f, 0f);
				backButton.OnMouseOver += FadedMouseOver;
				backButton.OnMouseOut += FadedMouseOut;
				backButton.OnLeftClick += GoBack;
				outerContainer.Append(backButton);
			}
		}
		
		Append(outerContainer);
	}
	
	private static void FadedMouseOver(UIMouseEvent e, UIElement el)
	{
		SoundEngine.PlaySound(12);
		
		if (e.Target is not UIPanel panel)
		{
			return;
		}
		
		panel.BackgroundColor = new Color(73, 94, 171);
		panel.BorderColor = Colors.FancyUIFatButtonMouseOver;
	}
	
	private static void FadedMouseOut(UIMouseEvent e, UIElement el)
	{
		if (e.Target is not UIPanel panel)
		{
			return;
		}
		
		panel.BackgroundColor = new Color(63, 82, 151) * 0.7f;
		panel.BorderColor = Color.Black;
	}
	
	private static void GoBack(UIMouseEvent e, UIElement el)
	{
		Main.menuMode = 11;
	}
	
	private static void PopulateListWithLanguages(UIList list, UILanguageSelectMenu menu)
	{
		foreach (var culture in Languages.GetCultures())
		{
			list.Add(ElementFromGameCulture(culture, menu));
		}
	}
	
	private static UIGameCultureListItem ElementFromGameCulture(GameCulture culture, UILanguageSelectMenu menu)
	{
		var item = new UIGameCultureListItem(culture, menu);
		item.Width.Set(0f, 1f);
		item.Height.Set(30f, 0f);
		return item;
	}
}
