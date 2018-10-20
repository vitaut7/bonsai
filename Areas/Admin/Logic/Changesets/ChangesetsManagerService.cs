﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bonsai.Areas.Admin.ViewModels.Changesets;
using Bonsai.Areas.Admin.ViewModels.Media;
using Bonsai.Areas.Admin.ViewModels.Pages;
using Bonsai.Areas.Admin.ViewModels.Relations;
using Bonsai.Areas.Front.Logic;
using Bonsai.Code.DomainModel.Media;
using Bonsai.Code.Utils;
using Bonsai.Code.Utils.Helpers;
using Bonsai.Data;
using Bonsai.Data.Models;
using Impworks.Utils.Format;
using Impworks.Utils.Linq;
using Impworks.Utils.Strings;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace Bonsai.Areas.Admin.Logic.Changesets
{
    /// <summary>
    /// The service for searching and displaying changesets.
    /// </summary>
    public class ChangesetsManagerService
    {
        public ChangesetsManagerService(IEnumerable<IChangesetRenderer> renderers, AppDbContext db)
        {
            _db = db;
            _renderers = renderers.ToDictionary(x => x.EntityType, x => x);
        }

        private readonly AppDbContext _db;
        private readonly IReadOnlyDictionary<ChangesetEntityType, IChangesetRenderer> _renderers;

        #region Public methods

        /// <summary>
        /// Finds changesets.
        /// </summary>
        public async Task<ChangesetsListVM> GetChangesetsAsync(ChangesetsListRequestVM request)
        {
            const int PageSize = 20;

            request = NormalizeListRequest(request);

            var result = new ChangesetsListVM { Request = request };
            await FillAdditionalDataAsync(request, result);

            var query = _db.Changes
                           .AsNoTracking()
                           .Include(x => x.Author)
                           .Include(x => x.EditedPage)
                           .ThenInclude(x => x.MainPhoto)
                           .Include(x => x.EditedMedia)
                           .Include(x => x.EditedRelation)
                           .AsQueryable();

            if (!string.IsNullOrEmpty(request.SearchQuery))
            {
                var search = request.SearchQuery.ToLower();
                query = query.Where(x => (x.EditedPage != null && x.EditedPage.Title.ToLower().Contains(search))
                                         || x.EditedMedia != null && x.EditedMedia.Title.ToLower().Contains(search));
            }

            if (request.EntityTypes?.Length > 0)
                query = query.Where(x => request.EntityTypes.Contains(x.Type));

            if (request.EntityId != null)
                query = query.Where(x => x.EditedPageId == request.EntityId
                                         || x.EditedMediaId == request.EntityId
                                         || x.EditedRelationId == request.EntityId);

            var totalCount = await query.CountAsync();
            result.PageCount = (int) Math.Ceiling((double) totalCount / PageSize);

            if (request.OrderBy == nameof(Changeset.Author))
                query = query.OrderBy(x => x.Author.UserName, request.OrderDescending);
            else
                query = query.OrderBy(x => x.Date, request.OrderDescending);

            var changesets = await query.Skip(PageSize * request.Page)
                                        .Take(PageSize)
                                        .ToListAsync();

            result.Items = changesets.Select(x => new ChangesetTitleVM
                                     {
                                         Id = x.Id,
                                         Date = x.Date,
                                         ChangeType = GetChangeType(x),
                                         Author = x.Author.FirstName + " " + x.Author.LastName,
                                         EntityId = x.EditedPageId ?? x.EditedMediaId ?? x.EditedRelationId ?? Guid.Empty,
                                         EntityType = x.Type,
                                         EntityTitle = GetEntityTitle(x),
                                         EntityThumbnailUrl = GetEntityThumbnailUrl(x),
                                         PageType = GetPageType(x)
                                     })
                                     .ToList();

            return result;
        }

        /// <summary>
        /// Returns the details for a changeset.
        /// </summary>
        public async Task<ChangesetDetailsVM> GetChangesetDetailsAsync(Guid id)
        {
            var chg = await _db.Changes
                               .AsNoTracking()
                               .Include(x => x.Author)
                               .Include(x => x.EditedMedia)
                               .GetAsync(x => x.Id == id, "Правка не найдена");

            var renderer = _renderers[chg.Type];
            var prevData = await renderer.RenderValuesAsync(chg.OriginalState);
            var nextData = await renderer.RenderValuesAsync(chg.UpdatedState);

            return new ChangesetDetailsVM
            {
                Id = chg.Id,
                Author = chg.Author.FirstName + " " + chg.Author.LastName,
                Date = chg.Date,
                ChangeType = GetChangeType(chg),
                EntityType = chg.Type,
                ThumbnailUrl = chg.EditedMedia != null
                    ? MediaPresenterService.GetSizedMediaPath(chg.EditedMedia.FilePath, MediaSize.Small)
                    : null,
                Changes = GetDiff(prevData, nextData, renderer)
            };
        }

        /// <summary>
        /// Restores the contents of an entity to the state befor an edit.
        /// </summary>
        public async Task<IVersionable> GetReverseEditorStateAsync(Guid id)
        {
            var chg = await _db.Changes
                               .AsNoTracking()
                               .GetAsync(x => x.Id == id, "Правка не найдена");

            if (string.IsNullOrEmpty(chg.OriginalState))
                throw new OperationException("Правка не может быть отменена");

            switch (chg.Type)
            {
                case ChangesetEntityType.Media:
                    return JsonConvert.DeserializeObject<MediaEditorVM>(chg.OriginalState);

                case ChangesetEntityType.Page:
                    return JsonConvert.DeserializeObject<PageEditorVM>(chg.OriginalState);

                case ChangesetEntityType.Relation:
                    return JsonConvert.DeserializeObject<RelationEditorVM>(chg.OriginalState);

                default:
                    throw new ArgumentException($"Неизвестный тип сущности: {chg.Type}!");
            }
        }

        #endregion

        #region Private helpers

        /// <summary>
        /// Completes and\or corrects the search request.
        /// </summary>
        private ChangesetsListRequestVM NormalizeListRequest(ChangesetsListRequestVM vm)
        {
            if(vm == null)
                vm = new ChangesetsListRequestVM();

            var orderableFields = new[] { nameof(Changeset.Date), nameof(Changeset.Author) };
            if(!orderableFields.Contains(vm.OrderBy))
                vm.OrderBy = orderableFields[0];

            if(vm.Page < 0)
                vm.Page = 0;

            return vm;
        }

        /// <summary>
        /// Returns the descriptive title for the changeset. 
        /// </summary>
        private string GetEntityTitle(Changeset chg)
        {
            if (chg.EditedPage != null)
                return chg.EditedPage.Title;

            if (chg.EditedMedia != null)
                return chg.EditedMedia.Title ?? chg.EditedMedia.Type.GetEnumDescription();

            return chg.EditedRelation.Type.GetEnumDescription();
        }

        /// <summary>
        /// Returns the thumbnail URL for the changeset.
        /// </summary>
        private string GetEntityThumbnailUrl(Changeset chg)
        {
            var file = chg.EditedPage?.MainPhoto?.FilePath ?? chg.EditedMedia?.FilePath;
            if (file != null)
                return MediaPresenterService.GetSizedMediaPath(file, MediaSize.Small);

            return null;
        }

        /// <summary>
        /// Returns the changeset type.
        /// </summary>
        private ChangesetType GetChangeType(Changeset chg)
        {
            if (chg.RevertedChangesetId != null)
                return ChangesetType.Restored;

            var wasNull = string.IsNullOrEmpty(chg.OriginalState);
            var isNull = string.IsNullOrEmpty(chg.UpdatedState);

            if(wasNull)
                return ChangesetType.Created;

            if(isNull)
                return ChangesetType.Removed;

            return ChangesetType.Updated;
        }

        /// <summary>
        /// Returns the page type (if any).
        /// </summary>
        private PageType? GetPageType(Changeset chg)
        {
            return chg.EditedPage?.Type;
        }

        /// <summary>
        /// Returns the list of diffed values.
        /// </summary>
        private IReadOnlyList<ChangeVM> GetDiff(IReadOnlyList<ChangePropertyValue> prevData, IReadOnlyList<ChangePropertyValue> nextData, IChangesetRenderer renderer)
        {
            if(prevData.Count != nextData.Count)
                throw new InvalidOperationException("Internal error: rendered changeset values mismatch!");

            var result = new List<ChangeVM>();

            for (var idx = 0; idx < prevData.Count; idx++)
            {
                var prevValue = prevData[idx].Value;
                var nextValue = nextData[idx].Value;

                if (prevValue == nextValue)
                    continue;

                var diff = renderer.GetCustomDiff(prevData[idx].PropertyName, prevValue, nextValue)
                           ?? new HtmlDiff.HtmlDiff(prevValue ?? "", nextValue ?? "").Build();

                result.Add(new ChangeVM
                {
                    Title = prevData[idx].Title,
                    Diff = diff
                });
            }

            return result;
        }

        /// <summary>
        /// Returns the additional filter data.
        /// </summary>
        private async Task FillAdditionalDataAsync(ChangesetsListRequestVM request, ChangesetsListVM data)
        {
            if (!string.IsNullOrEmpty(request.UserId))
            {
                var user = await _db.Users
                                    .Where(x => x.Id == request.UserId)
                                    .Select(x => new {x.FirstName, x.LastName})
                                    .FirstOrDefaultAsync();

                if (user != null)
                    data.UserTitle = user.FirstName + " " + user.LastName;
                else
                    request.UserId = null;
            }

            if (request.EntityId != null)
            {
                var title = await GetPageTitleAsync()
                            ?? await GetMediaTitleAsync();

                if (title != null)
                    data.EntityTitle = title;
                else
                    request.EntityId = null;
            }

            async Task<string> GetPageTitleAsync()
            {
                return await _db.Pages
                                .Where(x => x.Id == request.EntityId)
                                .Select(x => x.Title)
                                .FirstOrDefaultAsync();
            }

            async Task<string> GetMediaTitleAsync()
            {
                var media = await _db.Media
                                     .Where(x => x.Id == request.EntityId)
                                     .Select(x => new {Title = x.Title})
                                     .FirstOrDefaultAsync();

                return media == null
                    ? null
                    : StringHelper.Coalesce(media.Title, "Медиа");
            }
        }

        #endregion
    }
}
