﻿using System;
using System.Web.Mvc;
using NUnit.Framework;
using Roadkill.Core;
using Roadkill.Core.AmazingConfig;
using Roadkill.Core.Configuration;
using Roadkill.Core.Database;
using Roadkill.Core.Mvc.Controllers;
using Roadkill.Core.Mvc.ViewModels;
using Roadkill.Core.Services;
using Roadkill.Tests.Unit.StubsAndMocks;

namespace Roadkill.Tests.Unit.Mvc.Controllers
{
	[TestFixture]
	[Category("Unit")]
	public class HelpControllerTests
	{
		private MocksAndStubsContainer _container;

		private ConfigurationStoreMock _configurationStore;
		private IConfiguration _configuration;

		private IUserContext _context;
		private UserServiceMock _userService;
		private PageService _pageService;

		private HelpController _helpController;
		private PageRepositoryMock _pageRepository;

		[SetUp]
		public void Setup()
		{
			_container = new MocksAndStubsContainer();
			_configurationStore = _container.ConfigurationStoreMock;
			_configuration = _container.Configuration;

			_context = _container.UserContext;

			_pageRepository = _container.PageRepository;
			_userService = _container.UserService;
			_pageService = _container.PageService;

			_helpController = new HelpController(_configurationStore, _userService, _context, _pageService);
		}

		[Test]
		public void index_should_return_viewresult()
		{
			// Arrange
			_configuration.MarkupType = "Mediawiki";

			// Act
			ViewResult result = _helpController.Index() as ViewResult;

			// Assert
			Assert.That(result, Is.Not.Null);
		}

		[Test]
		public void about_should_return_viewresult_and_page_with_about_tag_as_model()
		{
			// Arrange
			Page aboutPage = new Page()
			{
				Id = 1,
				Title = "about",
				Tags = "about"
			};

			_pageRepository.AddNewPage(aboutPage, "text", "nobody", DateTime.Now);

			// Act
			ViewResult result = _helpController.About() as ViewResult;

			// Assert
			Assert.That(result, Is.Not.Null);

			PageViewModel model = result.ModelFromActionResult<PageViewModel>();
			Assert.NotNull(model, "Null model");
			Assert.That(model.Id, Is.EqualTo(aboutPage.Id));
			Assert.That(model.Title, Is.EqualTo(aboutPage.Title));
		}

		[Test]
		public void about_should_return_redirectresult_to_new_page_when_no_page_has_about_tag()
		{
			// Arrange

			// Act
			RedirectToRouteResult result = _helpController.About() as RedirectToRouteResult;

			// Assert
			Assert.That(result, Is.Not.Null);
			Assert.That(result.RouteValues["controller"], Is.EqualTo("Pages"));
			Assert.That(result.RouteValues["action"], Is.EqualTo("New"));
			Assert.That(result.RouteValues["title"], Is.EqualTo("about"));
			Assert.That(result.RouteValues["tags"], Is.EqualTo("about"));
		}

		[Test]
		public void creolereference_should_return_view()
		{
			// Arrange

			// Act
			ViewResult result = _helpController.CreoleReference() as ViewResult;

			// Assert
			Assert.That(result, Is.Not.Null);
		}

		[Test]
		public void mediawikireference_should_return_view()
		{
			// Arrange

			// Act
			ViewResult result = _helpController.MediaWikiReference() as ViewResult;

			// Assert
			Assert.That(result, Is.Not.Null);
		}

		[Test]
		public void markdownreference_should_return_view()
		{
			// Arrange

			// Act
			ViewResult result = _helpController.MarkdownReference() as ViewResult;

			// Assert
			Assert.That(result, Is.Not.Null);
		}
	}
}
