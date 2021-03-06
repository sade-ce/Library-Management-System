﻿using LibraryData.Models;
using System.Collections.Generic;

namespace LibraryData
{
    public interface ILibraryAssetService
    {
        IEnumerable<LibraryAsset> GetAll();
        LibraryAsset GetById(int id);
        Book GetBookById(int id);
        Video GetVideoById(int id);

        void Add(LibraryAsset newAsset);
        void Update(LibraryAsset newAsset);
        void Delete(LibraryAsset newAsset);
        string GetAuthorOrDirector(int id);
        string GetType(int id);
        string GetTitle(int id);
        string GetIsbn(int id);

        LibraryBranch GetCurrentLocation(int id);

    }
}
