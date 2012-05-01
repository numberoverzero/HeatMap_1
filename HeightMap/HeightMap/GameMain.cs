using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace HeightMap
{

    static class Program{
        static void Main(string[] args)
        {
            using (GameMain game = new GameMain())
                game.Run();
        }
    }

    public class GameMain : Microsoft.Xna.Framework.Game{
        static int resolution = 9;
        static int size = (int)Math.Pow(2, resolution) + 1; 
        
        GraphicsDeviceManager graphics;
        SpriteBatch batch;
        SpriteFont debugFont;
        Random random;

        Map heightMap;
        List<Texture2D> colorMaps;
        int colorMapIndex;
        Texture2D pixel1x1;

        Camera camera;
        Vector2 cameraPos;
        float panAmount = 4.0f;
        Input input;

        float tileCount = 3;
        float space = 200;

        public GameMain()
        {
            graphics = new GraphicsDeviceManager(this);
            graphics.PreferredBackBufferWidth = graphics.PreferredBackBufferHeight = 512;
            random = new Random(0);
            Content.RootDirectory = "Content";
        }

        protected override void LoadContent()
        {
            batch = new SpriteBatch(GraphicsDevice);
            colorMaps = new List<Texture2D>();
            colorMaps.Add(Content.Load<Texture2D>("Maps/ColorMapCorrect"));
            colorMaps.Add(Content.Load<Texture2D>("Maps/ColorMap"));
            colorMaps.Add(Content.Load<Texture2D>("Maps/Midnight"));
            colorMapIndex = 0;
            debugFont = Content.Load<SpriteFont>("DebugFont");

            Color[] pixelColor = new Color[] { Color.Gray };
            pixel1x1 = new Texture2D(GraphicsDevice, 1, 1);
            pixel1x1.SetData(pixelColor);
            
            heightMap = new Map(Content, GraphicsDevice, colorMaps[colorMapIndex], size, size);
            heightMap.ThreadedGenerateRandomHeight(HeightFunction);

            input = new Input();
            input.AddKeyBinding("quit", Keys.Escape);
            input.AddKeyBinding("generate", Keys.Space);
            input.AddKeyBinding("zoom_out", Keys.Q);
            input.AddKeyBinding("zoom_in", Keys.E);
            input.AddKeyBinding("pan_up", Keys.W);
            input.AddKeyBinding("pan_down", Keys.S);
            input.AddKeyBinding("pan_left", Keys.A);
            input.AddKeyBinding("pan_right", Keys.D);
            input.AddKeyBinding("adj_space_up", Keys.Up);
            input.AddKeyBinding("adj_space_down", Keys.Down);
            input.AddKeyBinding("cycle_map", Keys.Tab);

            camera = new Camera(GraphicsDevice.Viewport);
            cameraPos = new Vector2(size / 2);
            camera.LockPosition(cameraPos, true);
        }

        protected override void Update(GameTime gameTime)
        {
            input.Update();
            camera.Update(gameTime.ElapsedGameTime.Milliseconds / 1000f);
            if (input.IsKeyBindingActive("quit"))
                this.Exit();
            if (input.IsKeyBindingPress("generate"))
                heightMap.ThreadedGenerateRandomHeight(HeightFunction);
            if (input.IsKeyBindingActive("pan_up"))
                cameraPos.Y -= panAmount/camera.Scale.Y;
            if (input.IsKeyBindingActive("pan_down"))
                cameraPos.Y += panAmount / camera.Scale.Y;
            if (input.IsKeyBindingActive("pan_left"))
                cameraPos.X -= panAmount / camera.Scale.X;
            if (input.IsKeyBindingActive("pan_right"))
                cameraPos.X += panAmount / camera.Scale.X;     
            if (input.IsKeyBindingActive("zoom_out"))
                camera.Scale /= 1.05f;
            if (input.IsKeyBindingActive("zoom_in"))
                camera.Scale *= 1.05f;
            if (input.IsKeyBindingActive("adj_space_up"))
                space *= 1.15f;
            if (input.IsKeyBindingActive("adj_space_down"))
                space /= 1.15f;
            if (input.IsKeyBindingPress("cycle_map"))
            {
                colorMapIndex++;
                colorMapIndex %= colorMaps.Count();
                heightMap.SetColorMap(colorMaps[colorMapIndex]);
            }
            camera.LockPosition(cameraPos, true);
            camera.AdvanceFrame();
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            var heightMapTexture = heightMap.GetTexture();

            batch.Begin(0, BlendState.AlphaBlend, null, null, null, null, camera.TransformMatrix);
            Vector2 tilePosition = Vector2.Zero;
            for(int i=0;i<tileCount;i++)
                for (int j = 0; j < tileCount; j++)
                {
                    tilePosition.X = j * (space + size);
                    tilePosition.Y = i * (space + size);
                    batch.Draw(heightMapTexture, tilePosition, Color.White);
                }
            
            batch.End();

            if (heightMap.IsGenerating)
            {
                String text = "Generating...";
                Vector2 textDimensions = debugFont.MeasureString(text); 
                float fontHeight = debugFont.MeasureString(text).Y;
                Vector2 screenDimensions = new Vector2(GraphicsDevice.Viewport.Width,
                                                       GraphicsDevice.Viewport.Height);
                Vector2 textPos = screenDimensions - textDimensions;
                batch.Begin();
                batch.Draw(pixel1x1, textPos, null, Color.White, 0, Vector2.Zero, textDimensions, SpriteEffects.None, 0);
                batch.DrawString(debugFont, text, textPos, Color.White);
                batch.End();
            }
            base.Draw(gameTime);
        }

        public float HeightFunction(float min, float max, int iteration)
        {
            float powerOffset = (float)Math.Pow(2, -iteration);
            float randomValue = (float)random.NextDouble();
            return MathHelper.Lerp(min * powerOffset, max * powerOffset, randomValue);
        }
    }
}
