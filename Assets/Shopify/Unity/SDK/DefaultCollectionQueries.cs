namespace Shopify.Unity.SDK {
    using System.Collections.Generic;
    using System;
    using Shopify.Unity.GraphQL;

    /// <summary>
    /// Generates default queries for <see ref="ShopifyClient.collections">ShopifyClient.collections </see>.
    /// </summary>
    public class DefaultCollectionQueries {
        public void ShopCollections(QueryRootQuery query, Dictionary<string, int> imageResolutions, int first = 250, string after = null) {
            query.shop(s => s
                .collections(cc => cc
                    .edges(e => e
                        .node(c => Collection(c, imageResolutions))
                        .cursor()
                    )
                    .pageInfo(pi => pi
                        .hasNextPage()
                    ),
                    first : first, after : after
                )
            );
        }

        public void Collection(CollectionQuery collection, Dictionary<string, int> imageResolutions) {
            collection
                .id()
                .image(pci => pci
                    .altText()
                    .transformedSrc()
                )
                .title()
                .description()
                .descriptionHtml()
                .updatedAt()
                .products(pc => ProductConnection(pc),
                    first : DefaultQueries.MaxPageSize
                );

            foreach (string alias in imageResolutions.Keys) {
                collection
                    .image(pci => pci
                        .altText()
                        .transformedSrc(),
                        maxWidth : imageResolutions[alias],
                        maxHeight : imageResolutions[alias],
                        alias : alias
                    );
            }
        }

        public void ProductConnection(ProductConnectionQuery productConnection) {
            productConnection
                .edges(e => e
                    .node(p => Product(p))
                    .cursor()
                )
                .pageInfo(pi => pi
                    .hasNextPage()
                );
        }

        public void Product(ProductQuery product) {
            product
                .id();
        }
    }
}
