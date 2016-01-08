﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Text.RegularExpressions;
using Roadkill.Core.AmazingConfig;
using Roadkill.Core.Text.Sanitizer;
using Roadkill.Core.Database;
using Roadkill.Core.Database.Repositories;
using Roadkill.Core.Text;
using Roadkill.Core.Plugins;

namespace Roadkill.Core.Converters
{
	/// <summary>
	/// A factory class for converting the system's markup syntax to HTML.
	/// </summary>
	public class MarkupConverter
	{
		private static Regex _imgFileRegex = new Regex("^File:", RegexOptions.IgnoreCase);
		private static Regex _anchorRegex = new Regex("(?<hash>(#|%23).+)", RegexOptions.IgnoreCase);

		private IConfiguration _configuration;
		private readonly IPageRepository _pageRepository;
		private IMarkupParser _parser;
		private List<string> _externalLinkPrefixes;
		private IPluginFactory _pluginFactory;
		
		/// <summary>
		/// The current <see cref="IMarkupParser"/> being used by this instance, which is taken from 
		/// the configuration markdown type setting.
		/// </summary>
		public IMarkupParser Parser
		{
			get { return _parser; }
		}

		/// <summary>
		/// Used to resolve the full path of urls for pages in the markup.
		/// </summary>
		public UrlResolver UrlResolver { get; set; }

		/// <summary>
		/// Creates a new markdown parser which handles the image and link parsing by the various different 
		/// markdown format parsers.
		/// </summary>
		/// <returns>An <see cref="IMarkupParser"/> for Creole,Markdown or Media wiki formats.</returns>
		public MarkupConverter(IConfiguration configuration, IPageRepository pageRepository,  IPluginFactory pluginFactory)
		{
			_externalLinkPrefixes = new List<string>()
			{
				"http://",
				"https://",
				"www.",
				"mailto:",
				"#",
				"tag:"
			};

			_pluginFactory = pluginFactory;
			_pageRepository = pageRepository;
			_configuration = configuration;

			// Create the UrlResolver for all wiki urls
			HttpContextBase httpContext = null;
			if (HttpContext.Current != null)
				httpContext = new HttpContextWrapper(HttpContext.Current);

			UrlResolver = new UrlResolver(httpContext);		
	
			if (!_configuration.Installed)
			{
				// Skip the chain of creation, as the markup converter isn't needed but is created by
				// StructureMap - this is required for installation
				return;
			}

			CreateParserForMarkupType();
		}

		private void CreateParserForMarkupType()
		{
			string markupType = "";
			if (!string.IsNullOrEmpty(_configuration.MarkupType))
			{
				markupType = _configuration.MarkupType.ToLower();
			}

			switch (markupType)
			{
				case "creole":
					_parser = new CreoleParser(_configuration);
					break;

				case "mediawiki":
					throw new NotImplementedException("Sorry, Mediawiki markup is no longer supported.");
					break;

				case "markdown":
					default:
					_parser = new MarkdownParser();
					break;
			}

			_parser.LinkParsed += LinkParsed;
			_parser.ImageParsed += ImageParsed;
		}

		/// <summary>
		/// Converts the menu markup to HTML.
		/// </summary>
		public string ParseMenuMarkup(string menuMarkup)
		{
			return _parser.Transform(menuMarkup);
		}

		/// <summary>
		/// Turns the wiki markup provided into HTML.
		/// </summary>
		/// <param name="text">A wiki markup string, e.g. creole markup.</param>
		/// <returns>The wiki markup converted to HTML.</returns>
		public PageHtml ToHtml(string text)
		{
			CustomTokenParser tokenParser = new CustomTokenParser(_configuration.InternalSettings);
			PageHtml pageHtml = new PageHtml();
			TextPluginRunner runner = new TextPluginRunner(_pluginFactory);

			// Text plugins before parse
			text = runner.BeforeParse(text, pageHtml);			

			// Parse the markup into HTML
			string html = _parser.Transform(text);
			
			// Remove bad HTML tags
			html = RemoveHarmfulTags(html);

			// Customvariables.xml file
			html = tokenParser.ReplaceTokensAfterParse(html);

			// Text plugins after parse
			html = runner.AfterParse(html);

			// Text plugins pre and post #container HTML
			pageHtml.PreContainerHtml = runner.PreContainerHtml();
			pageHtml.PostContainerHtml = runner.PostContainerHtml();
			
			pageHtml.IsCacheable = runner.IsCacheable;
			pageHtml.Html = html;

			return pageHtml;
		}		

		/// <summary>
		/// Adds the attachments folder as a prefix to all image URLs before the HTML &lt;img&gt; tag is written.
		/// </summary>
		private void ImageParsed(object sender, ImageEventArgs e)
		{
			if (!e.OriginalSrc.StartsWith("http://") && !e.OriginalSrc.StartsWith("https://") && !e.OriginalSrc.StartsWith("www."))
			{
				string src = e.OriginalSrc;
				src = _imgFileRegex.Replace(src, "");

				string attachmentsPath = _configuration.AttachmentSettings.GetAttachmentsUrlPath();
				string urlPath = attachmentsPath + (src.StartsWith("/") ? "" : "/") + src;
				e.Src = UrlResolver.ConvertToAbsolutePath(urlPath);
			}
		}

		/// <summary>
		/// Handles internal links, and the 'attachment:' prefix for attachment links.
		/// </summary>
		private void LinkParsed(object sender, LinkEventArgs e)
		{
			if (!_externalLinkPrefixes.Any(x => e.OriginalHref.StartsWith(x)))
			{
				e.IsInternalLink = true;

				// Parse internal links, including attachments, tag: and special: links
				string href = e.OriginalHref;
				string lowerHref = href.ToLower();

				if (lowerHref.StartsWith("attachment:") || lowerHref.StartsWith("~/"))
				{
					ConvertAttachmentHrefToFullPath(e);
				}
				else if (lowerHref.StartsWith("special:"))
				{
					ConvertSpecialPageHrefToFullPath(e);
				}
				else
				{
					ConvertInternalLinkHrefToFullPath(e);
				}
			}
			else
			{
				// Add the external-link class to all outward bound links, 
				// except for anchors pointing to <a name=""> tags on the current page.
				if (!e.OriginalHref.StartsWith("#"))
					e.CssClass = "external-link";
			}
		}

		/// <summary>
		/// Updates the LinkEventArgs.Href to be a full path to the attachment
		/// </summary>
		private void ConvertAttachmentHrefToFullPath(LinkEventArgs e)
		{
			string href = e.OriginalHref;
			string lowerHref = href.ToLower();

			if (lowerHref.StartsWith("attachment:"))
			{
				// Remove the attachment: part
				href = href.Remove(0, 11);
				if (!href.StartsWith("/"))
					href = "/" + href;
			}
			else if (lowerHref.StartsWith("~/"))
			{
				// Remove the ~ 
				href = href.Remove(0, 1);
			}

			// Get the full path to the attachment
			string attachmentsPath = _configuration.AttachmentSettings.GetAttachmentsUrlPath();
			e.Href = UrlResolver.ConvertToAbsolutePath(attachmentsPath) + href;
		}

		/// <summary>
		/// Updates the LinkEventArgs.Href to be a full path to the Special: page
		/// </summary>
		private void ConvertSpecialPageHrefToFullPath(LinkEventArgs e)
		{
			string href = e.OriginalHref;
			e.Href = UrlResolver.ConvertToAbsolutePath("~/wiki/"+href);
		}

		/// <summary>
		/// Updates the LinkEventArgs.Href to be a full path to the page, and the CssClass
		/// </summary>
		private void ConvertInternalLinkHrefToFullPath(LinkEventArgs e)
		{
			string href = e.OriginalHref;

			// Parse internal links
			string title = href;
			string anchorHash = "";

			// Parse anchors for other pages
			if (_anchorRegex.IsMatch(href))
			{
				// Grab the hash contents
				Match match = _anchorRegex.Match(href);
				anchorHash = match.Groups["hash"].Value;

				// Grab the url
				title = href.Replace(anchorHash, "");
			}

			if (Parser is MarkdownParser)
			{
				// For markdown, only urls with "-" in them are valid, spaces are ignored.
				// Remove these, so a match is made. No url has a "-" in, so replacing them is ok.
				title = title.Replace("-", " ");
			}

			// Find the page, or if it doesn't exist point to the new page url
			Page page = _pageRepository.GetPageByTitle(title);
			if (page != null)
			{
				href = UrlResolver.GetInternalUrlForTitle(page.Id, page.Title);
				href += anchorHash;
			}
			else
			{
				href = UrlResolver.GetNewPageUrlForTitle(href);
				e.CssClass = "missing-page-link";
			}

			e.Href = href;
			e.Target = "";
		}

		/// <summary>
		/// Strips a lot of unsafe Javascript/Html/CSS from the markup, if the feature is enabled.
		/// </summary>
		private string RemoveHarmfulTags(string html)
		{
			if (_configuration.UseHtmlWhiteList.GetValueOrDefault(true))
			{
				MarkupSanitizer sanitizer = new MarkupSanitizer(_configuration.InternalSettings.HtmlElementWhiteListPath, true, false, true);
				return sanitizer.SanitizeHtml(html);
			}
			else
			{
				return html;
			}
		}
	}
}
