﻿using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Events;
using Nop.Web.Framework.Events;
using Nop.Web.Framework.Mvc.Routing;
using Nop.Web.Models.Catalog;
using Nop.Web.Models.JsonLD;

namespace Nop.Web.Factories
{
    public partial class JsonLdModelFactory : IJsonLdModelFactory
    {
        #region Fields

        protected readonly IEventPublisher _eventPublisher;
        protected readonly INopUrlHelper _nopUrlHelper;
        protected readonly IWebHelper _webHelper;

        #endregion

        #region Ctor

        public JsonLdModelFactory(IEventPublisher eventPublisher,
            INopUrlHelper nopUrlHelper,
                IWebHelper webHelper)
        {
            _eventPublisher = eventPublisher;
            _nopUrlHelper = nopUrlHelper;
            _webHelper = webHelper;
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Prepare JsonLD breadcrumb list
        /// </summary>
        /// <param name="categoryBreadcrumb">List CategorySimpleModel</param>
        /// <returns>A task that represents the asynchronous operation
        /// The task result JsonLD Breadbrumb list
        /// </returns>
        protected async Task<JsonLdBreadcrumbList> PrepareJsonLdBreadCrumbListAsync(IList<CategorySimpleModel> categoryBreadcrumb)
        {
            var breadcrumbList = new JsonLdBreadcrumbList();
            var position = 1;

            foreach (var cat in categoryBreadcrumb)
            {
                var breadcrumbListItem = new JsonLdBreadcrumbListItem()
                {
                    Position = position,
                    Item = new JsonLdBreadcrumbItem()
                    {
                        Id = await _nopUrlHelper.RouteGenericUrlAsync<Category>(new { SeName = cat.SeName }, _webHelper.IsCurrentConnectionSecured() ? "https" : "http"),
                        Name = cat.Name
                    }
                };
                breadcrumbList.ItemListElement.Add(breadcrumbListItem);
                position++;
            }

            return breadcrumbList;
        }

        #endregion

        #region Methods
        /// <summary>
        /// Prepare category Breadcrum JsonLD
        /// </summary>
        /// <param name="categoryBreadcrumb">List CategorySimpleModel</param>
        /// <returns>A task that represents the asynchronous operation
        /// The task result JsonLD Breadbrumb list
        /// </returns>
        public async Task<JsonLdBreadcrumbList> PrepareJsonLdBreadCrumbCategoryAsync(IList<CategorySimpleModel> categoryBreadcrumb)
        {
            var breadcrumbList = await PrepareJsonLdBreadCrumbListAsync(categoryBreadcrumb);
            await _eventPublisher.ModelPreparedAsync(breadcrumbList);

            return breadcrumbList;
        }

        /// <summary>
        /// Prepare product breadcrum JsonLD
        /// </summary>
        /// <param name="breadcrumbModel">Product breadcrumb model</param>
        /// <returns>A task that represents the asynchronous operation
        /// The task result JsonLD breadcrumb list
        /// </returns>
        public async Task<JsonLdBreadcrumbList> PrepareJsonLdBreadCrumbProductAsync(ProductDetailsModel.ProductBreadcrumbModel breadcrumbModel)
        {
            var breadcrumbList = await PrepareJsonLdBreadCrumbListAsync(breadcrumbModel.CategoryBreadcrumb);
            breadcrumbList.ItemListElement.Add(
                new JsonLdBreadcrumbListItem()
                {
                    Position = breadcrumbList.ItemListElement.Count + 1,
                    Item = new JsonLdBreadcrumbItem()
                    {
                        Id = await _nopUrlHelper.RouteGenericUrlAsync<Category>(new { SeName = breadcrumbModel.ProductSeName }, _webHelper.IsCurrentConnectionSecured() ? "https" : "http"),
                        Name = breadcrumbModel.ProductName,
                    }
                });

            await _eventPublisher.ModelPreparedAsync(breadcrumbList);

            return breadcrumbList;
        }

        /// <summary>
        /// Prepare JsonLD product
        /// </summary>
        /// <param name="model">Product details model</param>
        /// <returns>A task that represents the asynchronous operation
        /// The task result JsonLD product
        /// </returns>
        public virtual async Task<JsonLdProduct> PrepareJsonLdProductAsync(ProductDetailsModel model)
        {
            var productUrl = await _nopUrlHelper.RouteGenericUrlAsync<Product>(new { SeName = model.SeName }, _webHelper.IsCurrentConnectionSecured() ? "https" : "http");
            var productPrice = model.AssociatedProducts.Any()
                ? model.AssociatedProducts.Min(associatedProduct => associatedProduct.ProductPrice.PriceValue)
                : model.ProductPrice.PriceValue;

            var product = new JsonLdProduct
            {
                Name = model.Name,
                Sku = model.Sku,
                Gtin = model.Gtin,
                Mpn = model.ManufacturerPartNumber,
                Description = model.ShortDescription,
                Image = model.DefaultPictureModel.ImageUrl
            };

            foreach (var manufacturer in model.ProductManufacturers)
            {
                product.Brand.Add(new JsonLdBrand() { Name = manufacturer.Name });
            }

            if (model.ProductReviewOverview.TotalReviews > 0)
            {
                var ratingPercent = 0;
                if (model.ProductReviewOverview.TotalReviews != 0)
                {
                    ratingPercent = ((model.ProductReviewOverview.RatingSum * 100) / model.ProductReviewOverview.TotalReviews) / 5;
                }
                var ratingValue = ratingPercent / (decimal)20;

                product.AggregateRating = new JsonLdAggregateRating
                {
                    RatingValue = ratingValue.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture),
                    ReviewCount = model.ProductReviewOverview.TotalReviews
                };
            }
            product.Offer = new JsonLdOffer()
            {
                Url = productUrl.ToString(),
                Price = model.ProductPrice.CallForPrice ? null : productPrice.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                PriceCurrency = model.ProductPrice.CurrencyCode,
                PriceValidUntil = model.AvailableEndDate,
                Availability = @"https://schema.org/" + (model.InStock ? "InStock" : "OutOfStock")
            };

            foreach (var associatedProduct in model.AssociatedProducts)
                product.IsSimilarTo.Add(await PrepareJsonLdProductAsync(associatedProduct));

            if (model.ProductReviewOverview.TotalReviews > 0)
            {
                foreach (var review in model.ProductReviews.Items)
                {
                    product.Review.Add(new JsonLdReview()
                    {
                        Name = review.Title,
                        ReviewBody = review.ReviewText,
                        ReviewRating = new JsonLdRating()
                        {
                            RatingValue = review.Rating
                        },
                        Author = new JsonLdPerson() { Name = review.CustomerName },
                        DatePublished = review.WrittenOnStr
                    });
                }
            }

            await _eventPublisher.ModelPreparedAsync(product);

            return product;
        }

        #endregion
    }
}
