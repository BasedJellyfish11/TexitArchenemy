using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord.Commands;
using System.IO;
using JetBrains.Annotations;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;
using TexitArchenemy.Services.Logger;


namespace TexitArchenemy.Services.Discord.Commands;

public class Skeleton: ModuleBase<SocketCommandContext>
{
    private static readonly Random random = new();
    private const int BOTTOM_MARGIN = 10;
    private const int MIDDLE_MARGIN = 100 + BOTTOM_MARGIN;
        
    [Command("Skeleton")]
    [Summary("These are cool")]
    [UsedImplicitly]
    public async Task skeletonCommand([Summary("The upper text")]string upperText, [Summary("The lower text")] string lowerText = "")
    {
        upperText = upperText.ToUpper();
        lowerText = lowerText.ToUpper();
            
        // Discover all skeletons and choose one at random
        IEnumerable<string> files = Directory.EnumerateFiles("Skeletons/").ToList();
        string path = files.ElementAt(random.Next(0, files.Count()));
        Image image = await Image.LoadAsync(path);
            
        DrawingOptions options = new()
        {

            TextOptions = new TextOptions()
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                WrapTextWidth = image.Width
            }
        };

        // Make sure the text fits by reducing the font until it does
        Font font = new(SystemFonts.Find("Impact"), Math.Min(image.Width, image.Height) / 6f, FontStyle.Bold);
        FontRectangle allTextRectangle;
        while ((allTextRectangle = TextMeasurer.Measure(string.Concat(upperText, Environment.NewLine, lowerText), new RendererOptions(font) { WrappingWidth = image.Width })).Height + MIDDLE_MARGIN > image.Height
               || allTextRectangle.Width > image.Width)
        {
            font = new Font(font, font.Size * 0.9f);
        }

        // Brush and outline
        SolidBrush? brush = Brushes.Solid(Color.White);
        Pen? outlinePen = Pens.Solid(Color.Black, font.Size/20);
            
        // Draw upper text at 0,0
        image.Mutate
        (
            x => x.DrawText
            (
                options,
                upperText,
                font,
                brush,
                outlinePen,
                new PointF(0, 0)
            )
        );

        FontRectangle bottomRectangleSize = TextMeasurer.Measure(string.Concat(lowerText), new RendererOptions(font) { WrappingWidth = image.Width });
        image.Mutate
        (
            x => x.DrawText
            (
                options,
                lowerText,
                font,
                brush,
                outlinePen,
                new PointF(0, image.Height - bottomRectangleSize.Height - BOTTOM_MARGIN) // Draw the bottom text as low as possible while still fitting the rectangle
            )
        );

        // Save to MemoryStream so that we don't need to store the file locally
        MemoryStream stream = new();
        await image.SaveAsync(stream, PngFormat.Instance);
        stream.Seek(0, SeekOrigin.Begin);
        await Context.Channel.SendFileAsync(stream, "skeleton.png");
            
        await ArchenemyLogger.Log($"Executed command Skeleton for user {Context.User} in channel {Context.Channel} {Context.Channel.Id}", "Discord");


    }
    
    
        
}