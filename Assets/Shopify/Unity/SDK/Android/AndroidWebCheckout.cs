namespace Shopify.Unity.SDK.Android {
#if UNITY_ANDROID
    using System;
    using Shopify.Unity.SDK;

#if !SHOPIFY_MONO_UNIT_TEST
    using UnityEngine;
#endif

    class AndroidWebCheckout : WebCheckout {
        private ShopifyClient _client;
        private Cart _cart;

        protected override ShopifyClient Client {
            get {
                return _client;
            }
        }

        protected override Cart Cart {
            get {
                return _cart;
            }
        }

        private const string UnityPlayerClassName = "com.unity3d.player.UnityPlayer";
        private const string WebCheckoutSessionClassName = "com.shopify.buy.web.WebCheckoutSession";

        public AndroidWebCheckout(Cart cart, ShopifyClient client) {
            _cart = cart;
            _client = client;
        }

        public override void Checkout(string checkoutURL, CheckoutSuccessCallback success, CheckoutCancelCallback cancelled, CheckoutFailureCallback failure) {
#if !SHOPIFY_MONO_UNIT_TEST
            using(var webCheckoutSession = new AndroidJavaObject(WebCheckoutSessionClassName, GetNativeMessageReceiverName(), checkoutURL)) {
                SetupWebCheckoutMessageReceiver(success, cancelled, failure);
                webCheckoutSession.Call("checkout");
            }
#endif
        }
    }
#endif
}