﻿// PROJECT : MsCrmTools.WebResourcesManager
// This project was developed by Tanguy Touzard
// CODEPLEX: http://xrmtoolbox.codeplex.com
// BLOG: http://mscrmtools.blogspot.com

using MsCrmTools.WebResourcesManager.AppCode;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Svg;

namespace MsCrmTools.WebResourcesManager.UserControls
{
    /// <summary>
    /// Control that displays an image web resource
    /// </summary>
    public partial class ImageControl : UserControl, IWebResourceControl
    {
        #region Variables

        /// <summary>
        /// Base64 content of the web resource when loading this control
        /// </summary>
        private readonly string originalContent;

        /// <summary>
        /// Base64 content of the web resource
        /// </summary>
        private string innerContent;

        private readonly WebResource resource;

        #endregion Variables

        #region Delegates

        public delegate void WebResourceUpdatedEventHandler(object sender, WebResourceUpdatedEventArgs e);

        #endregion Delegates

        #region Event Handlers

        public event EventHandler<WebResourceUpdatedEventArgs> WebResourceUpdated;

        #endregion Event Handlers

        #region Constructor

        /// <summary>
        /// Initializes a new instance of class ImageControl
        /// </summary>
        /// <param name="resource">Web resource</param>
        public ImageControl(WebResource resource)
        {
            InitializeComponent();

            this.resource = resource;

            originalContent = resource.EntityContent;
            innerContent = resource.EntityContent;
        }

        #endregion Constructor

        public WebResource Resource => resource;

        #region Handlers

        private void ImageControl_Load(object sender, EventArgs e)
        {
            try
            {
                if (innerContent == null) return;

                string imageBase64 = innerContent;
                byte[] imageBytes = Convert.FromBase64String(imageBase64);

                if (resource.EntityType == (int)Enumerations.WebResourceType.Vector)
                {
                    using (MemoryStream ms = new MemoryStream(imageBytes))
                    {
                        using (StreamReader reader = new StreamReader(ms))
                        {
                            using (var xmlStream = new MemoryStream(Encoding.Default.GetBytes(reader.ReadToEnd())))
                            {
                                xmlStream.Position = 0;
                                SvgDocument svgDoc = SvgDocument.Open<SvgDocument>(xmlStream);

                                pictureBox1.Height = 32;
                                pictureBox1.Width = 32;
                                pictureBox1.Image = svgDoc.Draw(32, 32);

                                pictureBox1.Location = new Point(
                                    panel1.Width / 2 - pictureBox1.Width / 2,
                                    panel1.Height / 2 - pictureBox1.Height / 2);

                                return;
                            }
                        }
                    }
                }

                using (MemoryStream ms = new MemoryStream(imageBytes))
                {
                    var image = Image.FromStream(ms, true, true);

                    FrameDimension FrameDimensions = new FrameDimension(image.FrameDimensionsList[0]);
                    int frames = image.GetFrameCount(FrameDimensions);

                    if (frames > 1)
                    {
                        pictureBox1.Visible = false;
                        lblInfo.Text = "This image is an animated GIF and cannot be rendered";
                        pnlInfo.Visible = true;
                    }
                    else
                    {
                        pictureBox1.Image = image;

                        pictureBox1.Height = pictureBox1.Image.Size.Height;
                        pictureBox1.Width = pictureBox1.Image.Size.Width;

                        if (pictureBox1.Width > panel1.Width)
                            pictureBox1.Width = panel1.Width;

                        pictureBox1.Location = new Point(
                            panel1.Width / 2 - pictureBox1.Width / 2,
                            panel1.Height / 2 - pictureBox1.Height / 2);
                    }
                }
            }
            catch (Exception error)
            {
                MessageBox.Show("An error occured while loading this web resource: " + error.Message, "Error",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            try
            {
                base.OnPaint(e);
            }
            catch (Exception error)
            {
                MessageBox.Show("Custom : " + error);
            }
        }

        #endregion Handlers

        #region Methods

        public string GetBase64WebResourceContent()
        {
            return innerContent;
        }

        public Enumerations.WebResourceType GetWebResourceType()
        {
            return (Enumerations.WebResourceType)resource.EntityType;
        }

        public void ReplaceWithNewFile(string filename)
        {
            try
            {
                innerContent = Convert.ToBase64String(File.ReadAllBytes(filename));
                ImageControl_Load(null, null);

                SendSavedMessage();
                resource.SetAsSaved();
            }
            catch (Exception error)
            {
                MessageBox.Show(ParentForm, "Error while updating file: " + error.Message, "Error",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SendSavedMessage()
        {
            var wrueArgs = new WebResourceUpdatedEventArgs
            {
                Base64Content = innerContent,
                IsDirty = innerContent != originalContent,
                Type = (Enumerations.WebResourceType)resource.EntityType
            };

            WebResourceUpdated?.Invoke(this, wrueArgs);
        }

        #endregion Methods
    }
}