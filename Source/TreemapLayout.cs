using System;
using System.Collections.Generic;
using System.Drawing;

namespace CDiskCleaner
{
    internal static class TreemapLayout
    {
        public static RectangleF[] Compute(IList<double> values, RectangleF bounds)
        {
            int n = values.Count;
            var rects = new RectangleF[n];
            if (n == 0 || bounds.Width <= 1f || bounds.Height <= 1f) return rects;

            double total = 0;
            for (int i = 0; i < n; i++)
            {
                if (values[i] < 0) values[i] = 0;
                total += values[i];
            }
            if (total <= 0) return rects;

            double scale = (double)bounds.Width * bounds.Height / total;
            var area = new double[n];
            for (int i = 0; i < n; i++) area[i] = values[i] * scale;

            int start = 0;
            RectangleF free = bounds;

            while (start < n)
            {
                if (area[start] <= 0.0001)
                {
                    start++;
                    continue;
                }

                double side = Math.Min(free.Width, free.Height);
                int end = start + 1;
                double rowSum = area[start];
                double worst = Worst(area, start, end, rowSum, side);

                while (end < n && area[end] > 0.0001)
                {
                    double sum2 = rowSum + area[end];
                    double w2 = Worst(area, start, end + 1, sum2, side);
                    if (w2 >= worst) break;
                    rowSum = sum2;
                    end++;
                    worst = w2;
                }

                if (free.Width >= free.Height)
                {
                    double width = rowSum / free.Height;
                    float x = free.X;
                    float y = free.Y;
                    for (int i = start; i < end; i++)
                    {
                        float h = (float)(area[i] / width);
                        rects[i] = new RectangleF(x, y, (float)width, h);
                        y += h;
                    }
                    free = new RectangleF(free.X + (float)width, free.Y, free.Width - (float)width, free.Height);
                }
                else
                {
                    double height = rowSum / free.Width;
                    float x = free.X;
                    float y = free.Y;
                    for (int i = start; i < end; i++)
                    {
                        float w = (float)(area[i] / height);
                        rects[i] = new RectangleF(x, y, w, (float)height);
                        x += w;
                    }
                    free = new RectangleF(free.X, free.Y + (float)height, free.Width, free.Height - (float)height);
                }

                start = end;
                if (free.Width <= 0.5f || free.Height <= 0.5f) break;
            }
            return rects;
        }

        private static double Worst(double[] area, int start, int end, double sum, double side)
        {
            double min = double.MaxValue;
            double max = 0;
            for (int i = start; i < end; i++)
            {
                if (area[i] < min) min = area[i];
                if (area[i] > max) max = area[i];
            }
            if (sum <= 0 || min <= 0) return double.MaxValue;
            double s2 = side * side;
            return Math.Max(s2 * max / (sum * sum), (sum * sum) / (s2 * min));
        }
    }
}
