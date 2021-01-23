using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Media.Imaging;
using TheArtOfDev.HtmlRenderer.WinForms;
using RazorEngine;
using RazorEngine.Templating;
using RazorEngine.Text;
using TheArtOfDev.HtmlRenderer.Core.Entities;
using Image = System.Drawing.Image;

// For extension methods.


// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo
// ReSharper disable CommentTypo

namespace fiphwinfo
{
   
    public class MyHtmlHelper
    {
        public IEncodedString Raw(string rawString)
        {
            return new RawString(rawString);
        }
    }

    public abstract class HtmlSupportTemplateBase<T> : TemplateBase<T>
    {
        public HtmlSupportTemplateBase()
        {
            Html = new MyHtmlHelper();
        }

        public MyHtmlHelper Html { get; set; }
    }

    internal class FipPanel
    {

        private readonly object _refreshDevicePageLock = new object();

        private bool _initOk;

        private int CurrentCard = 0;

        private const int DEFAULT_PAGE = 0;

        private int _currentLcdYOffset;
        private int _currentLcdHeight;

        public IntPtr FipDevicePointer;
        private string SerialNumber;

        private uint _prevButtons;

        private bool[] _ledState = new bool[7];

        private List<uint> _pageList = new List<uint>();

        private readonly Pen _scrollPen = new Pen(Color.FromArgb(0xff,0xFF,0xB0,0x00));
        private readonly Pen _whitePen = new Pen(Color.FromArgb(0xff, 0xFF, 0xFF, 0xFF),(float)0.1);
        
        
        private readonly SolidBrush _scrollBrush = new SolidBrush(Color.FromArgb(0xff, 0xFF, 0xB0, 0x00));
        private readonly SolidBrush _whiteBrush = new SolidBrush(Color.FromArgb(0xff, 0xFF, 0xFF, 0xFF));

        private readonly Font _drawFont = new Font("Arial", 13, GraphicsUnit.Pixel);

        private Image _htmlImage;
        private Image _cardcaptionHtmlImage;

        private const int HtmlWindowXOffset = 1;

        private int _htmlWindowWidth = 320;
        private int _htmlWindowHeight = 240;

        private int HtmlWindowUsableWidth => _htmlWindowWidth - 9 - HtmlWindowXOffset;

        private double ScrollBarHeight => _htmlWindowHeight -7.0;

        private int ChartImageDisplayWidth => _htmlWindowWidth - 25;

        private const int ChartImageDisplayHeight = 60;
        
        private DirectOutputClass.PageCallback _pageCallbackDelegate;
        private DirectOutputClass.SoftButtonCallback _softButtonCallbackDelegate;

        private bool _blockNextUpState;


        public FipPanel(IntPtr devicePtr) 
        {
            FipDevicePointer = devicePtr;
        }
        
        public void Initalize()
        {
            // FIP = 3e083cd8-6a37-4a58-80a8-3d6a2c07513e

            // https://github.com/Raptor007/Falcon4toSaitek/blob/master/Raptor007's%20Falcon%204%20to%20Saitek%20Utility/DirectOutput.h
            //https://github.com/poiuqwer78/fip4j-core/tree/master/src/main/java/ch/poiuqwer/saitek/fip4j

            _pageCallbackDelegate = PageCallback;
            _softButtonCallbackDelegate = SoftButtonCallback;

            var returnValues1 = DirectOutputClass.RegisterPageCallback(FipDevicePointer, _pageCallbackDelegate);
            if (returnValues1 != ReturnValues.S_OK)
            {
                App.Log.Error("FipPanel failed to init RegisterPageCallback. " + returnValues1);
            }
            var returnValues2 = DirectOutputClass.RegisterSoftButtonCallback(FipDevicePointer, _softButtonCallbackDelegate);
            if (returnValues2 != ReturnValues.S_OK)
            {
                App.Log.Error("FipPanel failed to init RegisterSoftButtonCallback. " + returnValues1);
            }

            var returnValues3 = DirectOutputClass.GetSerialNumber(FipDevicePointer, out SerialNumber);
            if (returnValues3 != ReturnValues.S_OK)
            {
                App.Log.Error("FipPanel failed to get Serial Number. " + returnValues1);
            }
            else
            {
                App.Log.Info("FipPanel Serial Number : " + SerialNumber);

                _initOk = true;

                AddPage(DEFAULT_PAGE, true);

                RefreshDevicePage();
            }

        }

        public void Shutdown()
        {
            try
            {
                if (_pageList.Count > 0)
                {
                    do
                    {
                        if (_initOk)
                        {
                            DirectOutputClass.RemovePage(FipDevicePointer, _pageList[0]);
                        }

                        _pageList.Remove(_pageList[0]);


                    } while (_pageList.Count > 0);
                }
            }
            catch (Exception ex)
            {
                App.Log.Error(ex);
            }

        }

        private void PageCallback(IntPtr device, IntPtr page, byte bActivated, IntPtr context)
        {
            if (device == FipDevicePointer)
            {
                if (bActivated != 0)
                {
                    RefreshDevicePage();
                }
            }
        }

        private void SoftButtonCallback(IntPtr device, IntPtr buttons, IntPtr context)
        {
            if (device == FipDevicePointer & (uint) buttons != _prevButtons)
            {
                var button = (uint) buttons ^ _prevButtons;
                var state = ((uint) buttons & button) == button;
                _prevButtons = (uint) buttons;

                //Console.WriteLine($"button {button}  state {state}");

                var mustRefresh = false;

                var mustRender = true;

                switch (button)
                {
                    case 8: // scroll clockwise
                        if (state)
                        {

                            CurrentCard++;
                            _currentLcdYOffset = 0;

                            mustRefresh = true;

                            var playSound = true;


                            if (playSound)
                            {
                                App.PlayClickSound();
                            }
                        }

                        break;
                    case 16: // scroll anti-clockwise

                        if (state)
                        {
                            CurrentCard--;
                            _currentLcdYOffset = 0;

                            mustRefresh = true;

                            var playSound = true;

                            if (playSound)
                            {
                                App.PlayClickSound();
                            }
                        }

                        break;
                    case 2: // scroll clockwise
                        _currentLcdYOffset += 50;

                        mustRender = false;

                        mustRefresh = true;

                        break;
                    case 4: // scroll anti-clockwise

                        if (_currentLcdYOffset == 0) return;

                        _currentLcdYOffset -= 50;
                        if (_currentLcdYOffset < 0)
                        {
                            _currentLcdYOffset = 0;
                        }

                        mustRender = false;

                        mustRefresh = true;

                        break;
                }

                if (!mustRefresh)
                {
                    if (state || !_blockNextUpState)
                    {
                        switch (button)
                        {
                            case 512:

                                CurrentCard++;
                                _currentLcdYOffset = 0;

                                mustRefresh = true;

                                App.PlayClickSound();

                                break;
                            case 1024:

                                CurrentCard--;
                                _currentLcdYOffset = 0;

                                mustRefresh = true;

                                App.PlayClickSound();

                                break;
                        }
                    }

                }

                _blockNextUpState = state;

                if (mustRefresh)
                {
                    RefreshDevicePage(mustRender);
                }

            }
        }

        private void CheckLcdOffset()
        {
            if (_currentLcdHeight <= _htmlWindowHeight)
            {
                _currentLcdYOffset = 0;
            }

            if (_currentLcdYOffset + _htmlWindowHeight > _currentLcdHeight )
            {
                _currentLcdYOffset = _currentLcdHeight - _htmlWindowHeight + 4;
            }

            if (_currentLcdYOffset < 0) _currentLcdYOffset = 0;
        }

        private ReturnValues AddPage(uint pageNumber, bool setActive)
        {
            var result = ReturnValues.E_FAIL;

            if (_initOk)
            {
                try
                {
                    if (_pageList.Contains(pageNumber))
                    {
                        return ReturnValues.S_OK;
                    }

                    result = DirectOutputClass.AddPage(FipDevicePointer, (IntPtr) pageNumber, string.Concat("0x", FipDevicePointer.ToString(), " PageNo: ", pageNumber), setActive);
                    if (result == ReturnValues.S_OK)
                    {
                        App.Log.Info("Page: " + pageNumber + " added");

                        _pageList.Add(pageNumber);
                    }
                }
                catch (Exception ex)
                {
                    App.Log.Error(ex);
                }
            }

            return result;
        }

        private ReturnValues SendImageToFip(uint page, Bitmap fipImage)
        {

            if (_initOk)
            {
                if (fipImage == null)
                {
                    return ReturnValues.E_INVALIDARG;
                }

                try
                {
                    fipImage.RotateFlip(RotateFlipType.Rotate180FlipX);

                    var bitmapData =
                        fipImage.LockBits(new Rectangle(0, 0, fipImage.Width, fipImage.Height),
                            ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
                    var intPtr = bitmapData.Scan0;
                    var local3 = bitmapData.Stride * fipImage.Height;
                    DirectOutputClass.SetImage(FipDevicePointer, page, 0, local3, intPtr);
                    fipImage.UnlockBits(bitmapData);
                    return ReturnValues.S_OK;
                }
                catch (Exception ex)
                {
                    App.Log.Error(ex);
                }
            }

            return ReturnValues.E_FAIL;
        }
        
        private void OnImageLoad(object sender, HtmlImageLoadEventArgs e)
        {

            try
            {
                var image = new Bitmap(ChartImageDisplayWidth, ChartImageDisplayHeight);

                using (var graphics = Graphics.FromImage(image))
                {
                    if (HWInfo.SensorTrends.ContainsKey(e.Src))
                    {
                        graphics.DrawLines(_scrollPen, HWInfo.SensorTrends[e.Src].Read(ChartImageDisplayWidth, ChartImageDisplayHeight));
                    }

                    graphics.DrawRectangle(_whitePen,
                        new Rectangle(0, 0, ChartImageDisplayWidth - 1, ChartImageDisplayHeight - 1));

                    graphics.DrawString(HWInfo.SensorTrends[e.Src].MaxV(), _drawFont, _whiteBrush, (float)1, (float)1);


                    graphics.DrawString(HWInfo.SensorTrends[e.Src].MinV(), _drawFont, _whiteBrush, (float)1, (float)ChartImageDisplayHeight-17);
                }

                e.Callback(image);

            }
            catch
            {
                var image = new Bitmap(1, 1);

                e.Callback(image);
            }
        }


        private void SetLed(uint ledNumber, bool state)
        {
            if (_ledState[ledNumber] != state)
            {
                DirectOutputClass.SetLed(FipDevicePointer, DEFAULT_PAGE,
                    ledNumber, state);
                _ledState[ledNumber] = state;
            }
        }

        public void RefreshDevicePage(bool mustRender = true)
        {

            lock (_refreshDevicePageLock)
            {
                using (var fipImage = new Bitmap(_htmlWindowWidth, _htmlWindowHeight))
                {
                    using (var graphics = Graphics.FromImage(fipImage))
                    {
                        var str = "";

                        if (CurrentCard < 0)
                        {
                            CurrentCard = 1;
                        }
                        else
                        if (CurrentCard > 1)
                        {
                            CurrentCard = 0;
                        }

                        if (mustRender)
                        {
                            try
                            {
                                lock (HWInfo.RefreshHWInfoLock)
                                {
                                    str =
                                        Engine.Razor.Run("hwinfo.cshtml", null, new
                                        {
                                            CurrentCard = CurrentCard,

                                            SensorCount = HWInfo.SensorData.Count,

                                            SensorData = HWInfo.SensorData.Values.ToList(),

                                            ChartImageDisplayWidth = ChartImageDisplayWidth,
                                            ChartImageDisplayHeight = ChartImageDisplayHeight

                                        });
                                }
                            }
                            catch (Exception ex)
                            {
                                App.Log.Error(ex);
                            }
                        }

                        graphics.Clear(Color.Black);

                        if (mustRender)
                        {
                            var measureData =HtmlRender.Measure(graphics, str, HtmlWindowUsableWidth, App.CssData,null, OnImageLoad);

                            _currentLcdHeight = (int)measureData.Height;
                        }

                        CheckLcdOffset();

                        if (_currentLcdHeight > 0)
                        {

                            if (mustRender)
                            {
                                _htmlImage = HtmlRender.RenderToImage(str,
                                    new Size(HtmlWindowUsableWidth, _currentLcdHeight + 20), Color.Black, App.CssData,
                                    null, OnImageLoad);
                            }

                            if (_htmlImage != null)
                            {
                                graphics.DrawImage(_htmlImage, new Rectangle(new Point(HtmlWindowXOffset, 0),
                                        new Size(HtmlWindowUsableWidth, _htmlWindowHeight + 20)),
                                    new Rectangle(new Point(0, _currentLcdYOffset),
                                        new Size(HtmlWindowUsableWidth, _htmlWindowHeight + 20)),
                                    GraphicsUnit.Pixel);
                            }
                        }

                        if (_currentLcdHeight > _htmlWindowHeight)
                        {
                            var scrollThumbHeight = _htmlWindowHeight / (double)_currentLcdHeight * ScrollBarHeight;
                            var scrollThumbYOffset = _currentLcdYOffset / (double)_currentLcdHeight * ScrollBarHeight;

                            graphics.DrawRectangle(_scrollPen, new Rectangle(new Point(_htmlWindowWidth - 9, 2),
                                                               new Size(5, (int)ScrollBarHeight)));

                            graphics.FillRectangle(_scrollBrush, new Rectangle(new Point(_htmlWindowWidth - 9, 2 + (int)scrollThumbYOffset),
                                new Size(5, 1 + (int)scrollThumbHeight)));

                        }
                        


                        if (mustRender)
                        {
                            var cardcaptionstr =
                                Engine.Razor.Run("cardcaption.cshtml", null, new
                                {
                                    CurrentCard = CurrentCard
                                });

                            _cardcaptionHtmlImage = HtmlRender.RenderToImage(cardcaptionstr,
                                new Size(HtmlWindowUsableWidth, 26), Color.Black, App.CssData, null,
                                null);
                        }

                        if (_cardcaptionHtmlImage != null)
                        {
                            graphics.DrawImage(_cardcaptionHtmlImage, HtmlWindowXOffset, 0);
                        }

                        SendImageToFip(DEFAULT_PAGE, fipImage);

                        if (_initOk)
                        {
                            for (uint i = 2; i <= 6; i++)
                            {
                                SetLed(i, false);
                            }

                            SetLed(5, true);
                            SetLed(6, true);

                        }

                    }
                }
            }
        }


    }
}
