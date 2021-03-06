﻿using System;
using System.IO;
using System.Linq;
using Hangfire;
using Library.Models.Catalog;
using Library.Models.Checkout;
using Library.Security;
using LibraryData;
using LibraryData.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
        private readonly ILogger<CatalogController> _logger;

        public CatalogController(
                        ILibraryAssetService assetsService,
                        IDataProtectionProvider dataProtectionProvider,
                        DataProtectionPurposeStrings dataProtectionPurposeStrings,
                        ILibraryBranch branch,
                        LibraryContext context,
                        IWebHostEnvironment webHostEnvironment,
                        ICheckout checkout,
                        ILogger<CatalogController> logger)
        {
            _assetsService = assetsService;
            protector = dataProtectionProvider.CreateProtector(dataProtectionPurposeStrings.AssetIdRouteValue);
            _branch = branch;
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _checkout = checkout;
            _logger = logger;
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
                    .Where(x => x.Title.ToUpper().Contains(searchString.ToUpper()) || x.AuthorOrDirector.ToUpper().Contains(searchString.ToUpper()))
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

                //Prevent exceptions while searching when the author of the book is unknown
                if (book.Author == null)
                {
                    book.Author = "-";
                }

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

                //Prevent exceptions while searching when the director of the video is unknown
                if (video.Director == null)
                {
                    video.Director = "-";
                }

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

        [HttpGet]
        [Authorize(Roles = "Admin, Employee")]
        public IActionResult EditBook(string id)
        {
            if (id == null)
            {
                return View("NoIdFound");
            }

            int decryptedId = Convert.ToInt32(protector.Unprotect(id));

            var asset = _assetsService.GetById(decryptedId);

            if (asset == null)
            {
                Response.StatusCode = 404;
                return View("AssetNotFound", decryptedId);
            }

            var model = new AssetEditBookViewModel
            {
                Id = id,
                Title = asset.Title,
                Author = _assetsService.GetAuthorOrDirector(decryptedId),
                ISBN = _assetsService.GetIsbn(decryptedId),
                Year = asset.Year,
                Cost = asset.Cost,
                ExistingPhotoPath = asset.ImageUrl,
                NumberOfCopies = asset.NumberOfCopies,
                LibraryBranchName = asset.Location.Name
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, Employee")]
        public IActionResult EditBook(AssetEditBookViewModel model)
        {
            if (ModelState.IsValid)
            {
                int decryptedId = Convert.ToInt32(protector.Unprotect(model.Id));
                var book = _assetsService.GetBookById(decryptedId);

                if (book == null)
                {
                    Response.StatusCode = 404;
                    return View("AssetNotFound", decryptedId);
                }

                book.Title = model.Title;
                book.Author = model.Author;
                book.Year = model.Year;
                book.ISBN = model.ISBN;
                book.Cost = model.Cost;
                book.NumberOfCopies = model.NumberOfCopies;

                if (model.Photo != null)
                {
                    if (model.ExistingPhotoPath != null)
                    {
                        string filePath = Path.Join(_webHostEnvironment.WebRootPath, model.ExistingPhotoPath);
                        System.IO.File.Delete(filePath);
                    }

                    string uniqueFileName = ProcessUploadedBookFile(model);

                    book.ImageUrl = "/images/" + uniqueFileName;
                }

                book.Location = _branch.GetBranchByName(model.LibraryBranchName);

                _assetsService.Update(book);

                return RedirectToAction("Detail", "Catalog", new { id = model.Id });
            }

            return View(model);
        }

        [HttpGet]
        [Authorize(Roles = "Admin, Employee")]
        public IActionResult EditVideo(string id)
        {
            if (id == null)
            {
                return View("NoIdFound");
            }

            int decryptedId = Convert.ToInt32(protector.Unprotect(id));

            var asset = _assetsService.GetById(decryptedId);

            if (asset == null)
            {
                Response.StatusCode = 404;
                return View("AssetNotFound", decryptedId);
            }

            var model = new AssetEditVideoViewModel
            {
                Id = id,
                Title = asset.Title,
                Director = _assetsService.GetAuthorOrDirector(decryptedId),
                Year = asset.Year,
                Cost = asset.Cost,
                ExistingPhotoPath = asset.ImageUrl,
                NumberOfCopies = asset.NumberOfCopies,
                LibraryBranchName = asset.Location.Name
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, Employee")]
        public IActionResult EditVideo(AssetEditVideoViewModel model)
        {
            if (ModelState.IsValid)
            {
                int decryptedId = Convert.ToInt32(protector.Unprotect(model.Id));

                var video = _assetsService.GetVideoById(decryptedId);

                if (video == null)
                {
                    Response.StatusCode = 404;
                    return View("AssetNotFound", decryptedId);
                }

                video.Title = model.Title;
                video.Director = model.Director;
                video.Year = model.Year;
                video.Cost = model.Cost;
                video.NumberOfCopies = model.NumberOfCopies;

                if (model.Photo != null)
                {
                    if (model.ExistingPhotoPath != null)
                    {
                        string filePath = Path.Join(_webHostEnvironment.WebRootPath, model.ExistingPhotoPath);
                        System.IO.File.Delete(filePath);
                    }

                    string uniqueFileName = ProcessUploadedVideoFile(model);

                    video.ImageUrl = "/images/" + uniqueFileName;
                }

                video.Location = _branch.GetBranchByName(model.LibraryBranchName);

                _assetsService.Update(video);

                return RedirectToAction("Detail", "Catalog", new { id = model.Id });
            }

            return View(model);
        }

        [HttpGet]
        [Authorize(Roles = "Admin, Employee")]
        public IActionResult Delete(string id)
        {
            if (id == null)
            {
                return View("NoIdFound");
            }

            int decryptedId = Convert.ToInt32(protector.Unprotect(id));

            ViewBag.DecryptedId = decryptedId;

            var asset = _assetsService.GetById(decryptedId);

            if (asset == null)
            {
                Response.StatusCode = 404;
                return View("AssetNotFound", decryptedId);
            }

            var model = new AssetEditBookViewModel
            {
                Id = id,
                Title = asset.Title,
                Author = _assetsService.GetAuthorOrDirector(decryptedId),
                ISBN = _assetsService.GetIsbn(decryptedId),
                Year = asset.Year,
                LibraryBranchName = asset.Location.Name
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, Employee")]
        public IActionResult DeleteConfirmed(string id)
        {
            int decryptedId = Convert.ToInt32(protector.Unprotect(id));

            var book = _assetsService.GetById(decryptedId);

            if (book == null)
            {
                Response.StatusCode = 404;
                return View("AssetNotFound", decryptedId);
            }

            try
            {
                _assetsService.Delete(book);
                return RedirectToAction("Index");
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex.Message);
                return RedirectToAction(nameof(Delete), new { id = id });
            }
        }

        [HttpGet]
        [Authorize(Roles = "Admin, Employee")]
        public IActionResult Checkout(string id)
        {
            if (id == null)
            {
                return View("NoIdFound");
            }

            int decryptedId = Convert.ToInt32(protector.Unprotect(id));

            var asset = _assetsService.GetById(decryptedId);

            if (asset == null)
            {
                Response.StatusCode = 404;
                return View("AssetNotFound", decryptedId);
            }

            var model = new CheckoutViewModel()
            {
                LibraryCardId = "",
                AssetId = id,
                Title = asset.Title,
                ImageUrl = asset.ImageUrl,
                IsCheckedOut = _checkout.IsCheckedOut(decryptedId)
            };

            return View(model);
        }

        [HttpPost]
        [Authorize(Roles = "Admin, Employee")]
        public IActionResult PlaceCheckout(string assetId, int libraryCardId)
        {
            int decryptedId = Convert.ToInt32(protector.Unprotect(assetId));

            _checkout.CheckOutItem(decryptedId, libraryCardId);

            return RedirectToAction("Detail", new { id = assetId });
        }

        [Authorize(Roles = "Admin, Employee")]
        public IActionResult CheckIn(string id)
        {
            if (id == null)
            {
                return View("NoIdFound");
            }

            int decryptedId = Convert.ToInt32(protector.Unprotect(id));

            _checkout.CheckInItem(decryptedId);

            return RedirectToAction("Detail", new { id = id });
        }

        [HttpGet]
        [Authorize(Roles = "Patron, Employee, Admin")]
        public IActionResult Hold(string id)
        {
            if (id == null)
            {
                return View("NoIdFound");
            }

            int decryptedId = Convert.ToInt32(protector.Unprotect(id));

            var asset = _assetsService.GetById(decryptedId);

            if (asset == null)
            {
                Response.StatusCode = 404;
                return View("AssetNotFound", decryptedId);
            }

            var model = new CheckoutViewModel()
            {
                LibraryCardId = "",
                AssetId = id,
                Title = asset.Title,
                ImageUrl = asset.ImageUrl,
                IsCheckedOut = _checkout.IsCheckedOut(decryptedId),
                HoldCount = _checkout.GetCurrentHolds(decryptedId).Count()
            };

            return View(model);
        }


        [HttpPost]
        [Authorize(Roles = "Patron, Employee, Admin")]
        public IActionResult PlaceHold(string assetId, int libraryCardId)
        {
            int decryptedId = Convert.ToInt32(protector.Unprotect(assetId));

            if (!_checkout.PlaceHold(decryptedId, libraryCardId))
            {
                return RedirectToAction("Hold", new { id = assetId });
            }

            var patron = _context.Users
                .FirstOrDefault(x => x.LibraryCard.Id == libraryCardId);

            var hold = _context.Holds
                .Include(x => x.LibraryCard)
                .Include(x => x.LibraryAsset)
                .FirstOrDefault(x => x.LibraryCard.Id == libraryCardId && x.LibraryAsset.Id == decryptedId);


            if (hold.FirstHold == true)
            {
                BackgroundJob.Enqueue<IEmailService>(x => x.SendEmailAsync(patron.FirstName, patron.Email, "Place hold on the book",
                $"You have placed hold on the asset: '{hold.LibraryAsset.Title}' from our library. " +
                "Now you have to come to us and take the item in 24 hours time. " +
                "If you will not take the item up to this time you will not be able to borrow it."));
            }


            return RedirectToAction("Detail", new { id = assetId });
        }

        [Authorize(Roles = "Admin, Employee")]
        public IActionResult MarkLost(string assetId)
        {
            if (assetId == null)
            {
                return View("NoIdFound");
            }

            int decryptedId = Convert.ToInt32(protector.Unprotect(assetId));

            _checkout.MarkLost(decryptedId);

            return RedirectToAction("Detail", new { id = assetId });
        }

        [Authorize(Roles = "Admin, Employee")]
        public IActionResult MarkFound(string assetId)
        {
            if (assetId == null)
            {
                return View("NoIdFound");
            }

            int decryptedId = Convert.ToInt32(protector.Unprotect(assetId));

            _checkout.MarkFound(decryptedId);

            return RedirectToAction("Detail", new { id = assetId });
        }
    }
}