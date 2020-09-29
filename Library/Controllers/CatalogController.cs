﻿using System;
using System.IO;
using System.Linq;
using Library.Models.Catalog;
using Library.Security;
using LibraryData;
using LibraryData.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace Library.Controllers
{
    public class CatalogController : Controller
    {
        private readonly ILibraryAssetService _assetsService;
        private readonly IDataProtector protector;
        private readonly ILibraryBranch _branch;
        private readonly LibraryContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ICheckout _checkout;

        public CatalogController(
                        ILibraryAssetService assetsService,
                        IDataProtectionProvider dataProtectionProvider,
                        DataProtectionPurposeStrings dataProtectionPurposeStrings,
                        ILibraryBranch branch,
                        LibraryContext context,
                        IWebHostEnvironment webHostEnvironment,
                        ICheckout checkout)
        {
            _assetsService = assetsService;
            protector = dataProtectionProvider.CreateProtector(dataProtectionPurposeStrings.AssetIdRouteValue);
            _branch = branch;
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _checkout = checkout;
        }

        [AllowAnonymous]
        public IActionResult Index(string searchString)
        {
            var assetModels = _assetsService.GetAll()
                .Select(x => 
                {
                    x.EncryptedId = protector.Protect(x.Id.ToString());
                    return x;
                })
                .OrderBy(x => x.Title);


            var listingResult = assetModels
                .Select(a => new AssetIndexListingViewModel
                {
                    Id = a.EncryptedId,
                    ImageUrl = a.ImageUrl,
                    AuthorOrDirector = _assetsService.GetAuthorOrDirector(a.Id),
                    Title = _assetsService.GetTitle(a.Id),
                    Type = _assetsService.GetType(a.Id)
                }).ToList();

            if (!String.IsNullOrEmpty(searchString))
            {
                listingResult = listingResult
                    .Where(x => x.Title.ToUpper().Contains(searchString.ToUpper()))
                    .ToList();
            }

            var model = new AssetIndexViewModel
            {
                Assets = listingResult
            };

            return View(model);
        }

        [HttpGet]
        [Authorize(Roles = "Admin, Employee")]
        public IActionResult Create()
        {
            return View();
        }

        [HttpGet]
        [Authorize(Roles = "Admin, Employee")]
        public IActionResult CreateBook()
        {
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Admin, Employee")]
        public IActionResult CreateBook(AssetCreateBookViewModel model)
        {
            if (ModelState.IsValid)
            {
                string uniqueFileName = ProcessUploadedBookFile(model);

                var book = new Book
                {
                    Title = model.Title,
                    Author = model.Author,
                    ISBN = model.ISBN,
                    Year = model.Year,
                    Status = _context.Statuses.FirstOrDefault(x => x.Name == "Available"),
                    Cost = model.Cost,
                    ImageUrl = "/images/" + uniqueFileName,
                    NumberOfCopies = model.NumberOfCopies,
                    Location = _branch.GetBranchByName(model.LibraryBranchName)
                };

                _assetsService.Add(book);

                return RedirectToAction("Create", "Catalog");
            }

            return View(model);
        }

        private string ProcessUploadedBookFile(AssetCreateBookViewModel model)
        {
            string uniqueFileName = null;

            if (model.Photo != null)
            {
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images");
                uniqueFileName = Guid.NewGuid().ToString() + "_" + model.Photo.FileName;
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    model.Photo.CopyTo(fileStream);
                }
            }

            return uniqueFileName;
        }

        [HttpGet]
        [Authorize(Roles = "Admin, Employee")]
        public IActionResult CreateVideo()
        {
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Admin, Employee")]
        public IActionResult CreateVideo(AssetCreateVideoViewModel model)
        {
            if (ModelState.IsValid)
            {
                string uniqueFileName = ProcessUploadedVideoFile(model);

                var video = new Video
                {
                    Title = model.Title,
                    Director = model.Director,
                    Year = model.Year,
                    Status = _context.Statuses.FirstOrDefault(x => x.Name == "Available"),
                    Cost = model.Cost,
                    ImageUrl = "/images/" + uniqueFileName,
                    NumberOfCopies = model.NumberOfCopies,
                    Location = _branch.GetBranchByName(model.LibraryBranchName)
                };

                _assetsService.Add(video);

                return RedirectToAction("Create", "Catalog");
            }

            return View(model);
        }

        private string ProcessUploadedVideoFile(AssetCreateVideoViewModel model)
        {
            string uniqueFileName = null;

            if (model.Photo != null)
            {
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images");
                uniqueFileName = Guid.NewGuid().ToString() + "_" + model.Photo.FileName;
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    model.Photo.CopyTo(fileStream);
                }
            }

            return uniqueFileName;
        }

        [AllowAnonymous]
        public IActionResult Detail(string id)
        {
            if (id == null)
            {
                return View("NoIdFound");
            }

            int decryptedId = Convert.ToInt32(protector.Unprotect(id));
            var asset = _assetsService.GetById(decryptedId);

            if (asset == null)
            {
                return View("AssetNotFound", decryptedId);
            }

            var currentHolds = _checkout.GetCurrentHolds(decryptedId)
                .Select(x => new AssetHoldModel
                {
                    PatronName = _checkout.GetCurrentHoldPatronName(x.Id),
                    HoldPlaced = _checkout.GetCurrentHoldPlaced(x.Id)
                });

            var model = new AssetDetailModel
            {
                AssetId = id,
                Title = asset.Title,
                AuthorOrDirector = _assetsService.GetAuthorOrDirector(decryptedId),
                Type = _assetsService.GetType(decryptedId),
                Year = asset.Year,
                ISBN = _assetsService.GetIsbn(decryptedId),
                Status = asset.Status.Name,
                Cost = asset.Cost,
                CurrentLocation = _assetsService.GetCurrentLocation(decryptedId).Name,
                ImageUrl = asset.ImageUrl,
                LatestCheckout = _checkout.GetLatestCheckout(decryptedId),
                PatronName = _checkout.GetCurrentCheckoutPatron(decryptedId),
                CheckoutHistory = _checkout.GetCheckoutHistory(decryptedId),
                CurrentHolds = currentHolds
            };

            return View(model);
        }
    }
}