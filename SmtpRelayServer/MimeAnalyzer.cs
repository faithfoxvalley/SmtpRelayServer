using Microsoft.Graph.Models;
using MimeKit;
using MimeKit.Text;
using MimeKit.Tnef;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace SmtpRelayServer
{
    // Reference: https://mimekit.net/docs/html/Working-With-Messages.htm
    internal class MimeAnalyzer : MimeVisitor
    {
        private readonly List<MimeEntity> attachments = new List<MimeEntity>();
        private string body;

        /// <summary>
        /// Creates a new MimeToExchange.
        /// </summary>
        public MimeAnalyzer()
        {
        }

        /// <summary>
        /// The list of attachments that were in the MimeMessage.
        /// </summary>
        public IList<MimeEntity> Attachments => attachments;

        /// <summary>
        /// The HTML string that can be set on the BrowserControl.
        /// </summary>
        public string HtmlBody => body ?? string.Empty;

        protected override void VisitMultipartAlternative(MultipartAlternative alternative)
        {
            // walk the multipart/alternative children backwards from greatest level of faithfulness to the least faithful
            for (int i = alternative.Count - 1; i >= 0 && body == null; i--)
                alternative[i].Accept(this);
        }

        // Edits html tags
        void HtmlTagCallback(HtmlTagContext ctx, HtmlWriter htmlWriter)
        {
            if (ctx.TagId == HtmlTagId.Meta && !ctx.IsEndTag)
            {
                bool isContentType = false;

                ctx.WriteTag(htmlWriter, false);

                // replace charsets with "utf-8" since our output will be in utf-8 (and not whatever the original charset was)
                foreach (HtmlAttribute attribute in ctx.Attributes)
                {
                    if (attribute.Id == HtmlAttributeId.Charset)
                    {
                        htmlWriter.WriteAttributeName(attribute.Name);
                        htmlWriter.WriteAttributeValue("utf-8");
                    }
                    else if (isContentType && attribute.Id == HtmlAttributeId.Content)
                    {
                        htmlWriter.WriteAttributeName(attribute.Name);
                        htmlWriter.WriteAttributeValue("text/html; charset=utf-8");
                    }
                    else
                    {
                        if (attribute.Id == HtmlAttributeId.HttpEquiv && attribute.Value != null
                            && attribute.Value.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                            isContentType = true;

                        htmlWriter.WriteAttribute(attribute);
                    }
                }
            }
            else
            {
                // pass the tag through to the output
                ctx.WriteTag(htmlWriter, true);
            }
        }

        protected override void VisitTextPart(TextPart entity)
        {
            TextConverter converter;

            if (body != null)
            {
                // since we've already found the body, treat this as an attachment
                AddAttachment(entity);
                return;
            }

            if (entity.IsHtml)
            {
                converter = new HtmlToHtml
                {
                    HtmlTagCallback = HtmlTagCallback
                };
            }
            else if (entity.IsFlowed)
            {
                FlowedToHtml flowed = new FlowedToHtml();

                if (entity.ContentType.Parameters.TryGetValue("delsp", out string delsp))
                    flowed.DeleteSpace = delsp.Equals("yes", StringComparison.OrdinalIgnoreCase);

                converter = flowed;
            }
            else
            {
                converter = new TextToHtml();
            }

            body = converter.Convert(entity.Text);
        }

        protected override void VisitTnefPart(TnefPart entity)
        {
            // extract any attachments in the MS-TNEF part
            foreach(MimeEntity e in entity.ExtractAttachments())
                AddAttachment(e);
        }

        protected override void VisitMessagePart(MessagePart entity)
        {
            // treat message/rfc822 parts as attachments
            AddAttachment(entity);
        }

        protected override void VisitMimePart(MimePart entity)
        {
            // realistically, if we've gotten this far, then we can treat this as an attachment
            // even if the IsAttachment property is false.
            AddAttachment(entity);
        }

        private void AddAttachment(MimeEntity attachment)
        {
            attachments.Add(attachment);
        }
    }
}
