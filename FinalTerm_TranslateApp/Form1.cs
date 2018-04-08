using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using OpenCvSharp;
using Patagames.Ocr;
using Patagames.Ocr.Enums;
using RestSharp;
using System.Net;

namespace FinalTerm_TranslateApp
{
    public partial class Form1 : Form
    {
        private string filename;
        private string targetText;
        List<Rect> sentense;

        private float calcBlurriness (Mat src)
        {
            Mat Gx = null, Gy = null;
            Cv2.Sobel(src, Gx, MatType.CV_32F, 1, 0);
            Cv2.Sobel(src, Gy, MatType.CV_32F, 0, 1);

            double normGx = Cv2.Norm(Gx);
            double normGy = Cv2.Norm(Gy);

            double sumSq = normGx * normGx + normGy * normGy;

            return (float)(1 / (sumSq / (src.Size().Width * src.Size().Height) + 1e-6));
        }


        private bool DownloadRemoteImageFile(string uri, string fileName)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            bool bImage = response.ContentType.StartsWith("image",
                StringComparison.OrdinalIgnoreCase);
            if ((response.StatusCode == HttpStatusCode.OK ||
                response.StatusCode == HttpStatusCode.Moved ||
                response.StatusCode == HttpStatusCode.Redirect) &&
                bImage)
            {
                using (System.IO.Stream inputStream = response.GetResponseStream())
                using (System.IO.Stream outputStream = System.IO.File.OpenWrite(fileName))
                {
                    byte[] buffer = new byte[4096];
                    int bytesRead;
                    do
                    {
                        bytesRead = inputStream.Read(buffer, 0, buffer.Length);
                        outputStream.Write(buffer, 0, bytesRead);
                    } while (bytesRead != 0);
                }

                return true;
            }
            else
            {
                return false;
            }
        }


        public Form1()
        {
            InitializeComponent();
            this.filename = null;
            this.targetText = "";
            sentense = null;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            
        }

        private void button1_Click(object sender, EventArgs e)
        {
            sentense = null;
            targetText = "";
            textBox1.Text = "";
            textBox2.Text = "";


            if (filename == null)
            {
                MessageBox.Show("이미지를 지정하지 않았습니다!");
                return;
            }

            // 검출한 문장 저장할 배열
            sentense = new List<Rect>();

            Mat image = new Mat(), // 원본 이미지
                image_bin = new Mat(), // 이진화 이미지
                prepro_img = new Mat(), // 전처리 이미지
                image3 = new Mat(), // 결과 이미지                
                drawing = new Mat();
            Rect temp_rect;


            OpenCvSharp.Point[][] contours;
            HierarchyIndex[] hierarchy;

            double ratio;

            int count = 0;

            image = Cv2.ImRead(this.filename);

            if (image == null)
            {
                MessageBox.Show("이미지를 불러오지 못했습니다!");
                return;
            }

            pictureBox1.Image = new Bitmap(OpenCvSharp.Extensions.BitmapConverter.ToBitmap(image));

            image.CopyTo(prepro_img);
            image.CopyTo(image3);
            // Modify Date : 2017/11/08 19:10
            // Start

            Cv2.CvtColor(image, image_bin, ColorConversionCodes.BGR2GRAY);
            Cv2.Laplacian(image, image3, MatType.CV_64F);

            Mat result64F = image3;
            image3.ConvertTo(result64F, MatType.CV_8U);

            pictureBox2.Image = new Bitmap(OpenCvSharp.Extensions.BitmapConverter.ToBitmap(image3));

            // End.


            /*
            // 이미지에서 픽셀의 RGB코드가 설정값을 만족하면 검은색, 만족하지 못하면 흰색으로 이진화
            MatOfByte3 mat3 = new MatOfByte3(prepro_img);
            var indexer = mat3.GetIndexer();

            byte r, g, b;
            
            for (int i = 0; i < prepro_img.Width; i++)
            {
                for(int j = 0; j < prepro_img.Height; j++)
                {
                    Vec3b color = indexer[j, i];
                    // RGB 3채널을 분리해서 픽셀의 rgb값을 획득함
                    // item0 = R, item1 = G, item2 = B
                    r = color.Item2;
                    g = color.Item1;
                    b = color.Item0;

                    if ((r < 30 && g < 30 && b < 30))
                    {
                        color.Item2 = 255; color.Item1 = 255; color.Item0 = 255;
                    }
                    else
                    {
                        color.Item2 = 0; color.Item1 = 0; color.Item0 = 0;
                    }
                    indexer[j, i] = color;
                }
            }

            prepro_img.CopyTo(image_bin);

            //Cv2.ImShow("Original", image_bin);

            // 3채널 -> 2채널
            Cv2.CvtColor(prepro_img, prepro_img, ColorConversionCodes.BGR2GRAY);
                        
            Cv2.Canny(prepro_img, prepro_img, 100, 300, 3);


            Cv2.FindContours(prepro_img, out contours, out hierarchy, RetrievalModes.Tree, ContourApproximationModes.ApproxSimple);
            OpenCvSharp.Point[][] contours_poly = new OpenCvSharp.Point[contours.Length][];


            List<Rect> boundRect = new List<Rect>();
            List<Rect> boundRect2 = new List<Rect>();

            for (int i = 0; i < contours.Length; i++)
            {
                contours_poly[i] = Cv2.ApproxPolyDP(contours[i], 1, true);
                boundRect.Add(Cv2.BoundingRect(contours_poly[i]));
            }

            drawing = OpenCvSharp.Mat.Zeros(prepro_img.Size(), MatType.CV_8UC3);

            // 윤곽선 좌표를 획득하고 Rect에 저장함
            for (int i = 0; i < contours.Length; i++)
            {
                ratio = (double)boundRect[i].Height / boundRect[i].Width;

                int boundRect_area = (boundRect[i].Width * boundRect[i].Height);

                if ((ratio <= 10) && (ratio >= 0.5) && (boundRect_area <= 800) && (boundRect_area >= 30))
                {
                    Cv2.Rectangle(drawing, boundRect[i].TopLeft, boundRect[i].BottomRight, Scalar.FromRgb(255, 0, 0), 1, LineTypes.Link8, 0);

                    boundRect2.Add(boundRect[i]);
                }
                                
                else if (ratio > 3 && (boundRect_area <= 400) && (boundRect_area >= 30))
                {
                    Cv2.Rectangle(drawing, boundRect[i].TopLeft, boundRect[i].BottomRight, Scalar.FromRgb(255, 0, 0), 1, LineTypes.Link8, 0);

                    boundRect2.Add(boundRect[i]);
                }
            }
            
            // x축 기준으로 Sorting
            for(int i = 0; i < boundRect2.Count; i++)
            {
                for(int j = 0; j < (boundRect2.Count - 1); j++)
                {
                    if (boundRect2[j].TopLeft.X > boundRect2[j + 1].TopLeft.X)
                    {
                        temp_rect = boundRect2[j];
                        boundRect2[j] = boundRect2[j + 1];
                        boundRect2[j + 1] = temp_rect;
                    }
                }
            }

            List<Rect> tempRect = new List<Rect>();
            Rect tailRect = new Rect();
            int dis_x = 0, dis_y = 0;
            int cnt = 0;
            double distance = 0;
            for(int i = 0; i< boundRect2.Count; i++)
            {
                for (int k = 0; k < sentense.Count; k++)
                {
                    if (sentense.Count != 0 && boundRect2[k].TopLeft.Y > sentense[0].TopLeft.Y - 5 && boundRect2[i].TopLeft.Y < sentense[k].BottomRight.Y + 5)
                        goto CONTINUE;
                }


                Cv2.Rectangle(image3, boundRect2[i].TopLeft, boundRect2[i].BottomRight, Scalar.FromRgb(255, 255, 0), 2, LineTypes.Link8, 0);

                count = 0;
                

                tailRect = boundRect2[i];
                for (int j = i + 1; j < boundRect2.Count - 1; j++)
                {
                    dis_x = boundRect2[j].TopLeft.X - tailRect.TopLeft.X;
                    dis_y = Math.Abs(boundRect2[j].TopLeft.Y - tailRect.TopLeft.Y);
                    
                    distance = tailRect.Width < 20 ? 8 : 4;

                    if (Math.Abs(dis_x) <= tailRect.Width * distance && dis_x >= tailRect.Width && dis_y < 10)
                    {
                        tempRect.Add(boundRect2[j]);
                        tailRect = boundRect2[j];
                        count++;
                        cnt++;
                        Cv2.Line(image3, boundRect2[i].TopLeft, tailRect.BottomRight,  Scalar.FromRgb(0, 0, 255), 1, LineTypes.Link8, 0);
                        Cv2.Rectangle(image3, tailRect.TopLeft, tailRect.BottomRight, Scalar.FromRgb(255, 255, 0), 2, LineTypes.Link8, 0);
                    }
                }
                if (cnt > 0 && (tailRect.TopLeft.Y - boundRect2[i].TopLeft.Y) < tailRect.Height)
                {
                    Cv2.Rectangle(image3, new Rect(boundRect2[i].TopLeft.X, boundRect2[i].TopLeft.Y, tailRect.BottomRight.X - boundRect2[i].TopLeft.X, boundRect2[i].Height), Scalar.FromRgb(0, 255, 0), 3, LineTypes.Link8, 0);
                    sentense.Add(new Rect(boundRect2[i].TopLeft.X, boundRect2[i].TopLeft.Y, tailRect.BottomRight.X - boundRect2[i].TopLeft.X, boundRect2[i].Height + 20));
                    continue;
                }
                cnt = 0;
                CONTINUE:
                {
                    continue;
                }
            }
            
            for (int i = 0; i < sentense.Count; i++)
            {
                Rect sent = sentense[i];
                if (sent.TopLeft.X < 1 || sent.TopLeft.Y < 1)
                    continue;
                else if (sent.BottomRight.X > image.Width || sent.BottomRight.Y > image.Height)
                    continue;
                Mat selectedImage = new Mat(image_bin, new Rect(sent.TopLeft.X, sent.TopLeft.Y - 2, sent.Width, sent.Height + 2));

                String result = GetText(new Bitmap(OpenCvSharp.Extensions.BitmapConverter.ToBitmap(selectedImage)));

                targetText += result + " ";

            }            
            
            textBox1.Text = targetText;
            textBox2.Text = Translate(targetText);

            pictureBox2.Image = new Bitmap(OpenCvSharp.Extensions.BitmapConverter.ToBitmap(image3));
            */
        }

        private void 파일ToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }
                
        private void 이미지불러오기LToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "이미지 불러오기";
            ofd.FileName = "test";
            ofd.Filter = "이미지 파일 (*.jpg, *.bmp, *.png) | *.jpg; *.bmp; *.png; | 모든 파일 (*.*) | *.*";

            DialogResult dr = ofd.ShowDialog();

            if (dr == DialogResult.OK)
            {
                filename = ofd.FileName;
            }
            if (filename != null)
            {
                pictureBox1.Image = Bitmap.FromFile(filename);
            }
        }
        
        public static string GetText(Bitmap imgsource)
        {
            string text;
            using (var api = OcrApi.Create())
            {
                api.Init(Languages.English);
                using (var bmp = imgsource)
                {
                    text = api.GetTextFromImage(bmp);
                }
            }

            return text;
        }

        public static string Translate(string target)
        {
            var client = new RestClient(" https://openapi.naver.com/v1/language/translate");
            var request = new RestRequest(Method.POST);
            request.AddHeader("content-type", "application/x-www-form-urlencoded");
            request.AddHeader("cache-control", "no-cache");
            request.AddHeader("charset", "UTF-8");


            request.AddHeader("X-Naver-Client-Id", "pciKpBn52kDwFKRXNMpf");
            request.AddHeader("X-Naver-Client-Secret", "_24vIIo1yV");
            request.AddParameter("application/x-www-form-urlencoded", "source=en&target=ko&text="+target, ParameterType.RequestBody);


            IRestResponse response = client.Execute(request);
            RestSharp.Deserializers.JsonDeserializer deserial = new RestSharp.Deserializers.JsonDeserializer();

            var JSONObj = deserial.Deserialize<Dictionary<string, Dictionary<string, object>>>(response);

            object test = JSONObj["message"]["result"];
            Dictionary<string, object> test2 = (Dictionary<string, object>)test;


            return test2["translatedText"].ToString();
            
        }

        private void button1_Click_1(object sender, EventArgs e)
        {
            
        }

        private void button1_Click_2(object sender, EventArgs e)
        {
          
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }
    }
}
 