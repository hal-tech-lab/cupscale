using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Numerics;
using System.Security.AccessControl;
using System.Threading.Tasks;
using System.Windows.Forms;
using Cupscale.IO;
using Cupscale.Main;
using Cupscale.OS;
using Cyotek.Windows.Forms;
using HTAlt.WinForms;
using ImageMagick;
using Paths = Cupscale.IO.Paths;

namespace Cupscale.UI
{
    internal class MainUIHelper
    {
        public enum Mode { Single, Interp, Chain, Advanced }
        public static Mode currentMode;
        public static ImageBox previewImg;

        public static Button model1;
        public static Button model2;

        public static int interpValue;

        public static ComboBox outputFormat;
        public static ComboBox overwrite;

        public static Image currentOriginal;
        public static Image currentOutput;

        public static int currentScale = 1;

        public static void Init(ImageBox imgBox, Button model1Btn, Button model2Btn, ComboBox formatBox, ComboBox overwriteBox)
        {
            previewImg = imgBox;
            model1 = model1Btn;
            model2 = model2Btn;
            outputFormat = formatBox;
            overwrite = overwriteBox;
        }

        public static async void UpscaleImage()
        {
            Program.mainForm.SetBusy(true);
            IOUtils.DeleteContentsOfDir(Paths.imgInPath);
            IOUtils.DeleteContentsOfDir(Paths.imgOutPath);
            Program.mainForm.SetProgress(3f, "Preprocessing...");
            if (!CopyImage())  // Try to copy/move image to input folder, return if failed
            {
                Cancel("I/O Error");
                return;
            }
            await ImageProcessing.ConvertImages(Paths.imgInPath, ImageProcessing.Format.PngFast, !Config.GetBool("alpha"), true, true);
            ModelData mdl = Upscale.GetModelData();
            await ESRGAN.UpscaleBasic(Paths.imgInPath, Paths.imgOutPath, mdl, Config.Get("tilesize"), bool.Parse(Config.Get("alpha")), ESRGAN.PreviewMode.None);
            await Upscale.Postprocessing();
            await Upscale.FilenamePostprocessing();
            await Upscale.CopyImagesTo(Path.GetDirectoryName(Program.lastFilename));
            Program.mainForm.SetProgress(0, "Done.");
            Program.mainForm.SetBusy(false);
        }

        static void Cancel(string reason = "")
        {
            if (string.IsNullOrWhiteSpace(reason))
                Program.mainForm.SetProgress(0f, "Cancelled.");
            else
                Program.mainForm.SetProgress(0f, "Cancelled: " + reason);
            string inputImgPath = Path.Combine(Paths.imgInPath, Path.GetFileName(Program.lastFilename));
            if (overwrite.SelectedIndex == 1 && File.Exists(inputImgPath) && !File.Exists(Program.lastFilename))    // Copy image back if overwrite mode was on
                File.Move(inputImgPath, Program.lastFilename);
        }

        public static bool HasValidModelSelection ()
        {
            bool valid = true;
            if (model1.Enabled && !File.Exists(Program.currentModel1))
                valid = false;
            if (model2.Enabled && !File.Exists(Program.currentModel2))
                valid = false;
            return valid;
        }

        static bool CopyImage()
        {
            try
            {
                //if (overwrite.SelectedIndex == 1)
                //    File.Move(Program.lastFilename, Path.Combine(Paths.imgInPath, Path.GetFileName(Program.lastFilename)));
                //else
                File.Copy(Program.lastFilename, Path.Combine(Paths.imgInPath, Path.GetFileName(Program.lastFilename)));
            }
            catch (Exception e)
            {
                MessageBox.Show("Error trying to copy/move file: \n\n" + e.Message, "Error");
                return false;
            }
            return true;
        }


        public static async void UpscalePreview(bool fullImage = false)
        {
            if (!HasValidModelSelection())
            {
                MessageBox.Show("Invalid model selection.\nMake sure you have selected a model and that the file still exists.", "Error");
                return;
            }
            Program.mainForm.SetBusy(true);
            Program.mainForm.SetProgress(3f, "Preparing...");
            Program.mainForm.resetState = new Cupscale.PreviewState(previewImg.Image, previewImg.Zoom, previewImg.AutoScrollPosition);
            ResetCachedImages();
            IOUtils.DeleteContentsOfDir(Paths.imgInPath);
            IOUtils.DeleteContentsOfDir(Paths.previewPath);
            IOUtils.DeleteContentsOfDir(Paths.previewOutPath);
            ESRGAN.PreviewMode prevMode = ESRGAN.PreviewMode.Cutout;
            if (fullImage)
            {
                prevMode = ESRGAN.PreviewMode.FullImage;
                if (!IOUtils.TryCopy(Paths.tempImgPath, Path.Combine(Paths.previewPath, "preview.png"), true)) return;
            }
            else
            {
                SaveCurrentCutout();
            }
            await Upscale.Preprocessing(Paths.previewPath);
            if (currentMode == Mode.Single)
            {
                string mdl1 = Program.currentModel1;
                if (string.IsNullOrWhiteSpace(mdl1)) return;
                ModelData mdl = new ModelData(mdl1, null, ModelData.ModelMode.Single);
                await ESRGAN.UpscaleBasic(Paths.previewPath, Paths.previewOutPath, mdl, Config.Get("tilesize"), bool.Parse(Config.Get("alpha")), prevMode);
            }
            if (currentMode == Mode.Interp)
            {
                string mdl1 = Program.currentModel1;
                string mdl2 = Program.currentModel2;
                if (string.IsNullOrWhiteSpace(mdl1) || string.IsNullOrWhiteSpace(mdl2)) return;
                ModelData mdl = new ModelData(mdl1, mdl2, ModelData.ModelMode.Interp, interpValue);
                await ESRGAN.UpscaleBasic(Paths.previewPath, Paths.previewOutPath, mdl, Config.Get("tilesize"), bool.Parse(Config.Get("alpha")), prevMode);
            }
            if (currentMode == Mode.Chain)
            {
                string mdl1 = Program.currentModel1;
                string mdl2 = Program.currentModel2;
                if (string.IsNullOrWhiteSpace(mdl1) || string.IsNullOrWhiteSpace(mdl2)) return;
                ModelData mdl = new ModelData(mdl1, mdl2, ModelData.ModelMode.Chain);
                await ESRGAN.UpscaleBasic(Paths.previewPath, Paths.previewOutPath, mdl, Config.Get("tilesize"), bool.Parse(Config.Get("alpha")), prevMode);
            }
            Program.mainForm.SetBusy(false);
        }


        public static void SaveCurrentCutout()
        {
            UIHelpers.ReplaceImageAtSameScale(previewImg, IOUtils.GetImage(Paths.tempImgPath));
            string path = Path.Combine(Paths.previewPath, "preview.png");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            GetCurrentRegion().Save(path);
        }

        public static Bitmap GetCurrentRegion()     // thx ieu
        {
            RectangleF sourceImageRegion = previewImg.GetSourceImageRegion();
            int num = (int)Math.Round(sourceImageRegion.Width);
            int num2 = (int)Math.Round(sourceImageRegion.Height);
            double zoomFactor = previewImg.ZoomFactor;
            int num3 = (int)Math.Round(SystemInformation.VerticalScrollBarWidth / zoomFactor);
            int num4 = (int)Math.Round(SystemInformation.HorizontalScrollBarHeight / zoomFactor);
            int num5 = (int)Math.Round(sourceImageRegion.Width * zoomFactor);
            int num6 = (int)Math.Round(sourceImageRegion.Height * zoomFactor);
            Size size = previewImg.GetInsideViewPort().Size;
            Logger.Log("Saving current region to bitmap. Offset: " + previewImg.AutoScrollPosition.X + "x" + previewImg.AutoScrollPosition.Y);
            PreviewMerger.offsetX = (float)previewImg.AutoScrollPosition.X / (float)previewImg.ZoomFactor;
            PreviewMerger.offsetY = (float)previewImg.AutoScrollPosition.Y / (float)previewImg.ZoomFactor;
            if (num5 <= size.Width)
            {
                num3 = 0;
            }
            if (num6 <= size.Height)
            {
                num4 = 0;
            }
            num += num3;
            num2 += num4;
            sourceImageRegion.Width = num;
            sourceImageRegion.Height = num2;
            Bitmap bitmap = new Bitmap(num, num2);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                graphics.DrawImage(previewImg.Image, new Rectangle(0, 0, num, num2), sourceImageRegion, GraphicsUnit.Pixel);
            }
            return bitmap;
        }

        public static SizeF GetCutoutSize()
        {
            SizeF cutoutSize = previewImg.GetSourceImageRegion().Size;
            cutoutSize.Width = (int)Math.Round(cutoutSize.Width);
            cutoutSize.Height = (int)Math.Round(cutoutSize.Height);
            return cutoutSize;
        }

        public static void ResetCachedImages()
        {
            currentOriginal = null;
            currentOutput = null;
        }

        public static void UpdatePreviewLabels(Label zoom, Label size, Label cutout)
        {
            int currScale = currentScale;
            int cutoutW = (int)GetCutoutSize().Width;
            int cutoutH = (int)GetCutoutSize().Height;
            zoom.Text = "Zoom: " + previewImg.Zoom + "% (Original: " + previewImg.Zoom * currScale + "%)";
            size.Text = "Size: " + previewImg.Image.Width + "x" + previewImg.Image.Height + " (Original: " + previewImg.Image.Width / currScale + "x" + previewImg.Image.Height / currScale + ")";
            cutout.Text = "Cutout: " + cutoutW + "x" + cutoutH + " (Original: " + cutoutW / currScale + "x" + cutoutH / currScale + ")";// + "% - Unscaled Size: " + previewImg.Image.Size * currScale + "%";
        }

        public static bool DroppedImageIsValid(string path)
        {
            try
            {
                MagickImage img = new MagickImage(path);
                if (img.Width > 4096 || img.Height > 4096)
                {
                    MessageBox.Show("Image is too big for the preview!\nPlease use images with less than 4096 pixels on either side.", "Error");
                    return false;
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("Failed to open image:\n\n" + e.Message, "Error");
                return false;
            }
            return true;
        }
    }
}