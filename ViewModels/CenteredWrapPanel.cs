using Avalonia.Controls;
using Avalonia;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Media;

namespace TournamentWizard.ViewModels
{
    public class CenteredWrapPanel : WrapPanel
    {
        protected override Size ArrangeOverride(Size finalSize)
        {
            double currentX = 0;
            double currentY = 0;
            double lineHeight = 0;
            var children = Children.ToList();
            List<Rect> childRects = new List<Rect>();

            // First pass: Determine the arrangement rectangles without centering
            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                var childSize = child.DesiredSize;

                if (currentX + childSize.Width > finalSize.Width)
                {
                    currentX = 0;
                    currentY += lineHeight;
                    lineHeight = 0;
                }

                childRects.Add(new Rect(currentX, currentY, childSize.Width, childSize.Height));

                currentX += childSize.Width;
                lineHeight = Math.Max(lineHeight, childSize.Height);
            }

            // Second pass: Calculate line widths and center the children
            currentY = 0;
            lineHeight = 0;
            double totalWidth = childRects.Sum(x => x.Width); // Calculate the total width of all items
            int lineStartIndex = 0;

            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                var childSize = child.DesiredSize;

                // Check if the current item would cause a line break or if it's the last item
                if (currentX + childSize.Width > finalSize.Width || i == children.Count - 1)
                {
                    // Calculate the total width of the current line
                    double lineWidth = childRects.GetRange(lineStartIndex, i - lineStartIndex + 1).Sum(x => x.Width);

                    // Calculate the starting X position to center the line
                    double startX = (finalSize.Width - lineWidth) / 2;

                    // Adjust for single-line arrangements
                    if (totalWidth <= finalSize.Width)
                    {
                        startX = 0; // Left align if all items fit on a single line
                    }

                    // Arrange the children on the current line
                    for (int j = lineStartIndex; j <= i; j++)
                    {
                        var rect = childRects[j];
                        children[j].Arrange(new Rect(startX + rect.X, rect.Y, rect.Width, rect.Height));
                    }

                    lineStartIndex = i + 1;
                    currentX = 0;
                    currentY += lineHeight;
                    lineHeight = 0;
                }
                else
                {
                    currentX += childSize.Width;
                    lineHeight = Math.Max(lineHeight, childSize.Height);
                }
            }

            return finalSize;
        }
    }
}
