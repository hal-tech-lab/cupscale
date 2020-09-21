﻿using Cupscale.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Cupscale.Forms
{
    public partial class SettingsForm : Form
    {
        public SettingsGuiCollection settings;
        public FormatsGuiCollection formats;

        public SettingsForm()
        {
            InitializeComponent();
            Show();
            CenterToScreen();
            settings = new SettingsGuiCollection(tilesize, alpha, modelPath, alphaBgColor, jpegExtension, useCpu);
            formats = new FormatsGuiCollection(jpegQ, webpQ);
        }

        private void SettingsForm_Load(object sender, EventArgs e)
        {
            Program.mainForm.Enabled = false;
            Logger.textbox = logTbox;
            //ConfigTabHelper.LoadSettings(tilesize, alpha, modelPath, alphaBgColor, jpegExtension, useCpu);
            LoadSettings();
        }

        void LoadSettings ()
        {
            Config.LoadGuiElement(tilesize);
            Config.LoadGuiElement(alpha);
            Config.LoadGuiElement(alphaBgColor);
            Config.LoadGuiElement(jpegExtension);
            Config.LoadGuiElement(useCpu);

            Config.LoadGuiElement(jpegQ);
            Config.LoadGuiElement(webpQ);
            Config.LoadGuiElement(ddsUseDxt);
            Config.LoadGuiElement(ddsMipsAmount);
        }

        private void SettingsForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveSettings();
            Program.mainForm.Enabled = true;
        }

        void SaveSettings()
        {
            Config.SaveGuiElement(tilesize);
            Config.SaveGuiElement(alpha);
            Config.SaveGuiElement(alphaBgColor);
            Config.SaveGuiElement(jpegExtension);
            Config.SaveGuiElement(useCpu);

            Config.SaveGuiElement(jpegQ);
            Config.SaveGuiElement(webpQ);
            Config.SaveGuiElement(ddsUseDxt);
            Config.SaveGuiElement(ddsMipsAmount);
        }

        private void confAlphaBgColorBtn_Click(object sender, EventArgs e)
        {
            alphaBgColorDialog.ShowDialog();
            string colorStr = ColorTranslator.ToHtml(Color.FromArgb(alphaBgColorDialog.Color.ToArgb())).Replace("#", "") + "FF";
            alphaBgColor.Text = colorStr;
            Config.Set("alphaBgColor", colorStr);
        }

        private void logTbox_VisibleChanged(object sender, EventArgs e)
        {
            if (logTbox.Visible)
                logTbox.Text = Logger.GetSessionLog();
        }
    }

    public struct SettingsGuiCollection
    {
        public ComboBox tilesize;
        public CheckBox alpha;
        public TextBox modelPath;
        public TextBox alphaColor;
        public TextBox jpegExt;
        public CheckBox useCpu;
        public SettingsGuiCollection (ComboBox tilesizeBox, CheckBox alphaBox, TextBox modelPathBox, TextBox alphaColorBox, TextBox jpegExtBox, CheckBox useCpuBox)
        {
            tilesize = tilesizeBox;
            alpha = alphaBox;
            modelPath = modelPathBox;
            alphaColor = alphaColorBox;
            jpegExt = jpegExtBox;
            useCpu = useCpuBox;
        }
    }

    public struct FormatsGuiCollection
    {
        public TextBox jpegQ;
        public TextBox webpQ;
        public FormatsGuiCollection(TextBox jpegQBox, TextBox webpQBox)
        {
            jpegQ = jpegQBox;
            webpQ = webpQBox;
        }   
    }
}