using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using System.Threading;


namespace HeightMap
{
    public class Map
    {

        protected int width, height;
        protected bool _suppressClamping;
        protected float[] data;
        protected bool dirty;

        protected bool isGenerating;
        public bool IsGenerating
        {
            get { return isGenerating; }
        }

        protected Texture2D colorMap;
        protected GraphicsDevice device;

        protected Effect colorMapEffect;
        protected SpriteBatch batch;
        RenderTarget2D textureCacheTarget;
        Texture2D intensityTexture;
        Color[] intensityColors;

        public Map(ContentManager content, GraphicsDevice device, Texture2D colorMap, int width, int height)
        {
            this.width = width;
            this.height = height;

            data = new float[width * height];
            dirty = true;
            _suppressClamping = false;
            isGenerating = false;
            this.device = device;
            colorMapEffect = content.Load<Effect>("ColorMapEffect");

            PresentationParameters pp = device.PresentationParameters;
            textureCacheTarget = new RenderTarget2D(device, width, height, false, pp.BackBufferFormat, pp.DepthStencilFormat, pp.MultiSampleCount, RenderTargetUsage.DiscardContents);
            batch = new SpriteBatch(device);

            SetColorMap(colorMap);
        }

        public float this[int row, int col]
        {
            get { return data[width * row + col]; }
            set
            {
                dirty = true;
                if (!_suppressClamping)
                    value = MathHelper.Clamp(value, 0, 1);
                data[width * row + col] = value;
            }
        }

        public void SetColorMap(Texture2D colorMap)
        {
            this.colorMap = colorMap;
            dirty = true;
        }
        public Texture2D GetTexture()
        {
            if (dirty && !isGenerating)
                GenerateTexture();
            return textureCacheTarget;
        }

        protected void GenerateTexture()
        {
            dirty = false;
            if(intensityTexture == null)
                intensityTexture = new Texture2D(device, width, height);
            if(intensityColors == null)
                intensityColors = new Color[width * height];
            float value;
            for (int i = 0; i < width * height; i++)
            {
                value = data[i];
                intensityColors[i] = new Color(value, value, value, value);
            }
            intensityTexture.SetData(intensityColors);
            device.Textures[0] = intensityTexture;
            device.Textures[1] = colorMap;

            device.SetRenderTarget(textureCacheTarget);
            batch.Begin(0, BlendState.Opaque, null, null, null, colorMapEffect);
            batch.Draw(intensityTexture, new Rectangle(0, 0, width, height), Color.White);
            batch.End();
            device.SetRenderTarget(null);
        }

        private void GenerateRandomHeight(Func<float, float, int, float> noiseFunction)
        {
            float noiseMin = -1;
            float noiseMax = 1;
            _suppressClamping = true;
            for (int i = 0; i < width * height; i++)
                data[i] = 0;
            float corner = noiseFunction(noiseMin, noiseMax, 0);
            this[0, 0] = this[0, height - 1] = this[width - 1, 0] = this[width - 1, height - 1] = corner;

            int x_min = 0;
            int y_min = 0;
            int x_max = width-1;
            int y_max = height-1;

            int side = x_max;
            int squares = 1;
            int offset = 1;

            int left, right, top, bottom, dx, dy, midX, midY, temp;
            while (side > 1){
                for (int i = 0; i < squares; i++){
                    for (int j = 0; j < squares; j++){
                        left = i * side;
                        right = (i + 1) * side;
                        top = j * side;
                        bottom = (j + 1) * side;

                        dx = dy = side / 2;

                        midX = left + dx;
                        midY = top + dy;

                        // Diamond step - create center average for each square
                        this[midX, midY] = Average(this[left, top],
                                                   this[left, bottom],
                                                   this[right, top],
                                                   this[right, bottom]);
                        this[midX, midY] += noiseFunction(noiseMin, noiseMax, offset);

                        // Square step - create squares for each diamond

                        // ==============
                        // Top Square
                        if (top - dy < y_min)
                            temp = y_max - dy;
                        else
                            temp = top - dy;
                        this[midX, top] = Average(this[left, top],
                                                  this[right, top],
                                                  this[midX, midY],
                                                  this[midX, temp]);
                        this[midX, top] += noiseFunction(noiseMin, noiseMax, offset);

                        // Top Wrapping
                        if (top == y_min)
                            this[midX, y_max] = this[midX, top];

                        // ==============
                        // Bottom Square
                        if (bottom + dy > y_max)
                            temp = top + dy;
                        else
                            temp = bottom - dy;
                        this[midX, bottom] = Average(this[left, bottom],
                                                     this[right, bottom],
                                                     this[midX, midY],
                                                     this[midX, temp]);
                        this[midX, bottom] += noiseFunction(noiseMin, noiseMax, offset);

                        // Bottom Wrapping
                        if (bottom == y_max)
                            this[midX, y_min] = this[midX, bottom];

                        // ==============
                        // Left Square
                        if (left - dx < x_min)
                            temp = x_max - dx;
                        else
                            temp = left - dx;
                        this[left, midY] = Average(this[left, top],
                                                   this[left, bottom],
                                                   this[midX, midY],
                                                   this[temp, midY]);
                        this[left, midY] += noiseFunction(noiseMin, noiseMax, offset);

                        // Left Wrapping
                        if (left == x_min)
                            this[x_max, midY] = this[left, midY];

                        // ==============
                        // Right Square
                        if (right + dx > x_max)
                            temp = x_min + dx;
                        else
                            temp = right + dx;
                        this[right, midY] = Average(this[right, top],
                                                    this[right, bottom],
                                                    this[midX, midY],
                                                    this[temp, midY]);
                        this[right, midY] += noiseFunction(noiseMin, noiseMax, offset);

                        // Right Wrapping
                        if (right == x_max)
                            this[x_min, midY] = this[right, midY];
                    }
                } //End for loops
                side /= 2;
                squares *= 2;
                offset += 1;
            }
            Normalize();
            _suppressClamping = false;
            isGenerating = false;
        }

        public void ThreadedGenerateRandomHeight(Func<float, float, int, float> noiseFunction)
        {
            if (isGenerating)
                return;
            isGenerating = true;
            Thread t = new Thread(() => { GenerateRandomHeight(noiseFunction); });
            t.Start();
        }
        private void Normalize()
        {
            float minValue = data.Min();
            float maxValue = data.Max();
            float range = maxValue - minValue;
            float pct = 0.05f;
            minValue -= range * pct / 2;
            maxValue += range * pct / 2;
            range = maxValue-minValue;
            for (int i = 0; i < width * height; i++)
                data[i] = (data[i] - minValue) / range;

            dirty = true;
        }
        private static float Average(params float[] values)
        {
            return values.Sum() / values.Count();
        }
    }
}
