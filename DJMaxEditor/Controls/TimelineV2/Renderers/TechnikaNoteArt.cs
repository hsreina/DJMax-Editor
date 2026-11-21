using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using DJMaxEditor.DJMax;

namespace DJMaxEditor.Controls.TimelineV2.Renderers
{
    internal static class TechnikaNoteArt
    {
        private const string ResourcePrefix = "DJMaxEditor.TechmaniaNotes.";

        private static readonly byte[] BasicPng = ReadResource("Basic.png");
        private static readonly byte[] ChainHeadPng = ReadResource("ChainHead.png");
        private static readonly byte[] ChainNodePng = ReadResource("ChainNode.png");
        private static readonly byte[] RepeatHeadPng = ReadResource("RepeatHead.png");
        private static readonly byte[] RepeatPng = ReadResource("Repeat.png");
        private static readonly byte[] HoldHeadPng = ReadResource("HoldHead.png");

        private static readonly Image BasicImage = CreateImage(BasicPng);
        private static readonly Image ChainHeadImage = CreateImage(ChainHeadPng);
        private static readonly Image ChainNodeImage = CreateImage(ChainNodePng);
        private static readonly Image RepeatHeadImage = CreateImage(RepeatHeadPng);
        private static readonly Image RepeatImage = CreateImage(RepeatPng);
        private static readonly Image HoldHeadImage = CreateImage(HoldHeadPng);
        private static readonly Dictionary<int, Image> ScaledImages =
            new Dictionary<int, Image>();
        private static readonly object ScaledImagesLock = new object();

        internal static byte[] GetPngBytes(TechnikaNoteKind kind)
        {
            byte[] source = BytesFor(kind);
            return source == null ? null : (byte[])source.Clone();
        }

        internal static bool TryDraw(
            Graphics graphics,
            EventData source,
            int startX,
            int centerY,
            int rowHeight)
        {
            TechnikaNoteKind kind = TechnikaNoteClassifier.Classify(source);
            return TryDraw(graphics, kind, startX, centerY, rowHeight, 1f);
        }

        internal static bool TryDraw(
            Graphics graphics,
            TechnikaNoteKind kind,
            int startX,
            int centerY,
            int rowHeight,
            float opacity = 1f)
        {
            if (ImageFor(kind) == null)
            {
                return false;
            }

            int size = Math.Min(48, Math.Max(14, rowHeight - 4));
            Image image = ScaledImageFor(kind, size);
            var destination = new Rectangle(
                startX - (size / 2),
                centerY - (size / 2),
                size,
                size);

            opacity = Math.Max(0f, Math.Min(1f, opacity));
            if (opacity >= 0.999f)
            {
                graphics.DrawImageUnscaled(image, destination.Location);
            }
            else
            {
                using (var attributes = new ImageAttributes())
                {
                    var matrix = new ColorMatrix { Matrix33 = opacity };
                    attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                    graphics.DrawImage(
                        image,
                        destination,
                        0,
                        0,
                        image.Width,
                        image.Height,
                        GraphicsUnit.Pixel,
                        attributes);
                }
            }

            return true;
        }

        /// <summary>
        /// The playfield variant: the head is centred on a floating point, scaled to the
        /// arcade frame size rather than a row height, may be semi-transparent (a note in
        /// the next scan reads at 60%), and a chain head turns to point at the member it
        /// joins. Falls back to false when no art exists, exactly like <see cref="TryDraw"/>.
        /// </summary>
        internal static bool TryDrawPlayfieldHead(
            Graphics graphics,
            TechnikaNoteKind kind,
            double centerX,
            double centerY,
            double size,
            double opacity,
            double angleDegrees)
        {
            if (ImageFor(kind) == null || size < 4.0)
            {
                return false;
            }

            Image image = ScaledImageFor(kind, Math.Max(4, (int)Math.Round(size)));
            var state = graphics.Save();
            try
            {
                graphics.TranslateTransform((float)centerX, (float)centerY);
                if (Math.Abs(angleDegrees) > 0.01)
                {
                    graphics.RotateTransform((float)angleDegrees);
                }
                var destination = new RectangleF(
                    (float)(-size / 2.0), (float)(-size / 2.0),
                    (float)size, (float)size);
                opacity = Math.Max(0.0, Math.Min(1.0, opacity));
                if (opacity >= 0.999)
                {
                    graphics.DrawImage(image, destination);
                }
                else
                {
                    using (var attributes = new ImageAttributes())
                    {
                        var matrix = new ColorMatrix { Matrix33 = (float)opacity };
                        attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                        graphics.DrawImage(
                            image,
                            Rectangle.Round(destination),
                            0f, 0f, (float)image.Width, (float)image.Height,
                            GraphicsUnit.Pixel,
                            attributes);
                    }
                }
            }
            finally
            {
                graphics.Restore(state);
            }

            return true;
        }

        private static Image ScaledImageFor(TechnikaNoteKind kind, int size)
        {
            int key = (AssetIdFor(kind) * 64) + size;
            lock (ScaledImagesLock)
            {
                Image scaled;
                if (ScaledImages.TryGetValue(key, out scaled))
                {
                    return scaled;
                }

                scaled = ScaleImage(ImageFor(kind), size);
                ScaledImages.Add(key, scaled);
                return scaled;
            }
        }

        private static Image ScaleImage(Image source, int size)
        {
            var scaled = new Bitmap(size, size, PixelFormat.Format32bppPArgb);
            using (Graphics graphics = Graphics.FromImage(scaled))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.DrawImage(source, new Rectangle(0, 0, size, size));
            }
            return scaled;
        }

        private static int AssetIdFor(TechnikaNoteKind kind)
        {
            switch (kind)
            {
                case TechnikaNoteKind.Basic:
                    return 1;
                case TechnikaNoteKind.Drag:
                case TechnikaNoteKind.ChainHead:
                    return 2;
                case TechnikaNoteKind.ChainNode:
                    return 3;
                case TechnikaNoteKind.RepeatHead:
                case TechnikaNoteKind.RepeatHeadHold:
                    return 4;
                case TechnikaNoteKind.Repeat:
                case TechnikaNoteKind.RepeatHold:
                    return 5;
                case TechnikaNoteKind.Hold:
                    return 6;
                default:
                    return 0;
            }
        }

        private static byte[] BytesFor(TechnikaNoteKind kind)
        {
            switch (kind)
            {
                case TechnikaNoteKind.Basic:
                    return BasicPng;
                case TechnikaNoteKind.Drag:
                case TechnikaNoteKind.ChainHead:
                    return ChainHeadPng;
                case TechnikaNoteKind.ChainNode:
                    return ChainNodePng;
                case TechnikaNoteKind.RepeatHead:
                case TechnikaNoteKind.RepeatHeadHold:
                    return RepeatHeadPng;
                case TechnikaNoteKind.Repeat:
                case TechnikaNoteKind.RepeatHold:
                    return RepeatPng;
                case TechnikaNoteKind.Hold:
                    return HoldHeadPng;
                default:
                    return null;
            }
        }

        private static Image ImageFor(TechnikaNoteKind kind)
        {
            switch (kind)
            {
                case TechnikaNoteKind.Basic:
                    return BasicImage;
                case TechnikaNoteKind.Drag:
                case TechnikaNoteKind.ChainHead:
                    return ChainHeadImage;
                case TechnikaNoteKind.ChainNode:
                    return ChainNodeImage;
                case TechnikaNoteKind.RepeatHead:
                case TechnikaNoteKind.RepeatHeadHold:
                    return RepeatHeadImage;
                case TechnikaNoteKind.Repeat:
                case TechnikaNoteKind.RepeatHold:
                    return RepeatImage;
                case TechnikaNoteKind.Hold:
                    return HoldHeadImage;
                default:
                    return null;
            }
        }

        private static byte[] ReadResource(string fileName)
        {
            Assembly assembly = typeof(TechnikaNoteArt).Assembly;
            using (Stream stream = assembly.GetManifestResourceStream(ResourcePrefix + fileName))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException(
                        "Missing embedded TECHMANIA note asset: " + fileName);
                }

                using (var memory = new MemoryStream())
                {
                    stream.CopyTo(memory);
                    return memory.ToArray();
                }
            }
        }

        private static Image CreateImage(byte[] png)
        {
            using (var stream = new MemoryStream(png))
            using (var source = Image.FromStream(stream))
            {
                return new Bitmap(source);
            }
        }
    }
}
