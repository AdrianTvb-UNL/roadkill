﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using Roadkill.Core.AmazingConfig;
using Roadkill.Core.Converters;
using Roadkill.Core.Localization;
using Roadkill.Core.Services;
using Roadkill.Core.Security;
using Roadkill.Core.Mvc.Attributes;
using Roadkill.Core.Mvc.ViewModels;

namespace Roadkill.Core.Mvc.Controllers
{
	/// <summary>
	/// Provides functionality that is common through the site.
	/// </summary>
	[OptionalAuthorization]
	public class HomeController : ControllerBase
	{
		public IPageService PageService { get; private set; }
		private readonly SearchService _searchService;
		private readonly MarkupConverter _markupConverter;

		public HomeController(IConfigurationStore configurationStore, UserServiceBase userManager, MarkupConverter markupConverter,
			IPageService pageService, SearchService searchService, IUserContext context)
			: base(configurationStore, userManager, context) 
		{
			_markupConverter = markupConverter;
			_searchService = searchService;
			PageService = pageService;
		}

		/// <summary>
		/// Display the homepage/mainpage. If no page has been tagged with the 'homepage' tag,
		/// then a dummy PageViewModel is put in its place.
		/// </summary>
		[BrowserCache]
		public ActionResult Index()
		{
			// Get the first locked homepage
			PageViewModel model = PageService.FindHomePage();

			if (model == null)
			{
				model = new PageViewModel();
				model.Title = SiteStrings.NoMainPage_Title;
				model.Content = SiteStrings.NoMainPage_Label;
				model.ContentAsHtml = _markupConverter.ToHtml(SiteStrings.NoMainPage_Label).Html;
				model.CreatedBy = "";
				model.CreatedOn = DateTime.UtcNow;
				model.RawTags = "homepage";
				model.ModifiedOn = DateTime.UtcNow;
				model.ModifiedBy = "";
			}

			return View(model);
		}

		/// <summary>
		/// Searches the lucene index using the search string provided.
		/// </summary>
		public ActionResult Search(string q)
		{
			ViewData["search"] = q;

			List<SearchResultViewModel> results = _searchService.Search(q).ToList();
			return View(results);
		}

		/// <summary>
		/// Returns Javascript 'constants' for the site.
		/// </summary>
		/// <param name="version">This is sent by the views to ensure new versions of Roadkill have this JS file cleared from the cache.</param>
		[CacheContentType(Duration = 86400 * 30, ContentType = "application/javascript")] // 30 days
		[AllowAnonymous]
		public ActionResult GlobalJsVars(string version)
		{
			return View();
		}

		/// <summary>
		/// Displays the left side menu view, including new page/settings if logged in.
		/// </summary>
		[AllowAnonymous]
		public ActionResult NavMenu()
		{
			return Content(PageService.GetMenu(Context));
		}

		/// <summary>
		/// Displays the a Bootstrap-styled left side menu view, including new page/settings if logged in.
		/// </summary>
		[AllowAnonymous]
		public ActionResult BootstrapNavMenu()
		{
			var pageService = PageService as PageService;
			if (pageService == null)
				return Content("");

			return Content(pageService.GetBootStrapNavMenu(Context));
		}
	}
}