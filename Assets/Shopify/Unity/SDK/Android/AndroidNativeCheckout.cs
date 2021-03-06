#if UNITY_ANDROID
namespace Shopify.Unity.SDK.Android {
    using System.Collections.Generic;
    using System.Collections;
    using System.Runtime.InteropServices;
    using System;
    using Shopify.Unity.MiniJSON;
    using Shopify.Unity.SDK;

#if !SHOPIFY_MONO_UNIT_TEST
    using UnityEngine;
#endif

    public partial class AndroidNativeCheckout : INativeCheckout {
        /// <value>Android class name used to communicate between Unity and Android.</value>
        private const string ANDROID_CLASS_NAME = "com.shopify.buy.androidpay.AndroidPayCheckoutSession";
        /// <value>State of the cart for the current checkout.</value>
        private CartState CartState;
        /// <value>The name of the merchant.</value>
        private String MerchantName;
        /// <value>The country code for the country where the shop is located.</value>
        private String CountryCodeString;
        /// <value>Callback instance to receive invocations from the Android plugin.</value>
        private AndroidPayEventReceiverBridge AndroidPayEventBridge;
#if !SHOPIFY_MONO_UNIT_TEST
        /// <value>Android object reference to invoke the Android plugin methods.</value>
        private AndroidJavaObject AndroidPayCheckoutSession;
#endif

        /// <value>External callback for checkout success.</value>
        private CheckoutSuccessCallback OnSuccess;
        /// <value>External callback for checkout cancelation.</value>
        private CheckoutCancelCallback OnCanceled;
        /// <value>External callback for checkout failure.</value>
        private CheckoutFailureCallback OnFailure;
        /// <value>Pending external callback for cals to
        /// <see cref="CanCheckout(PaymentSettings, CanCheckoutWithNativePayCallback)"/>.
        /// </value>
        private CanCheckoutWithNativePayCallback PendingCanCheckoutWithNativePayCallback;

        public AndroidNativeCheckout(CartState cartState) {
            CartState = cartState;
            const bool testing = true; // TODO parametrize for 3rd-party devs
#if !SHOPIFY_MONO_UNIT_TEST
            AndroidPayCheckoutSession = new AndroidJavaObject(
                ANDROID_CLASS_NAME,
                GlobalGameObject.Name,
                testing
            );
#endif
        }

        /// <summary>
        /// This method always return <code>false</code> on Android.
        /// The argument can be <code>null<code>.
        /// </summary>
        /// <returns>
        /// Always <code>false</code>.
        /// </returns>
        public bool CanShowPaymentSetup(PaymentSettings paymentSettings) {
            return false;
        }

        /// <summary>
        /// This method is not supported on Android, since
        /// <see cref="CanShowPaymentSetup(PaymentSettings)"/> always
        /// returns <code>false</code>.
        /// </summary>
        public void ShowPaymentSetup() { }

        /// <summary>
        /// Checks if Android Pay can be used as a checkout method on this device.
        /// </summary>
        /// <param name="paymentSettings">
        /// A <see cref="PaymentSettings"/> object used to read store payment options from.
        /// <param name="callback">
        /// A callback object to deliver the response to.
        /// </param>
        public void CanCheckout(PaymentSettings paymentSettings, CanCheckoutWithNativePayCallback callback) {
            if (paymentSettings.supportedDigitalWallets().Contains(DigitalWallet.ANDROID_PAY)) {
                object[] args = {
                    SerializedPaymentNetworksFromCardBrands(paymentSettings.acceptedCardBrands())
                };
#if !SHOPIFY_MONO_UNIT_TEST
                AndroidPayCheckoutSession.Call("canCheckoutWithAndroidPay", args);
#endif
                if (PendingCanCheckoutWithNativePayCallback != null) {
                    PendingCanCheckoutWithNativePayCallback(false);
                }
                PendingCanCheckoutWithNativePayCallback = callback;
            } else {
                callback(false);
            }
        }

        /// <summary>
        /// Starts the native checkout flow.
        /// </summary>
        /// <param name="key">
        /// A Base64-encoded Android Pay public key found on the Shopify admin page.
        /// </param>
        /// <param name="shopMetadata">
        /// Some metadata associated with the current shop, such as name and payment settings.
        /// </param>
        /// <param name="success">
        /// Callback to be invoked when the checkout flow is successfully completed.
        /// </param>
        /// <param name="canceled">
        /// Callback to be invoked when the checkout flow is canceled by the user.
        /// </param>
        /// <param name="failure">
        /// Callback to be called when the checkout flow fails.
        /// </param>
        public void Checkout(
            string key,
            ShopMetadata shopMetadata,
            CheckoutSuccessCallback success,
            CheckoutCancelCallback canceled,
            CheckoutFailureCallback failure
        ) {
            // TODO: Store callbacks and extract items we need from the cart to pass to Android Pay.
            OnSuccess = success;
            OnCanceled = canceled;
            OnFailure = failure;

            var checkout = CartState.CurrentCheckout;

            MerchantName = shopMetadata.Name; // TODO: Replace with Shop name
            var pricingLineItems = GetPricingLineItemsFromCheckout(checkout);
            var pricingLineItemsString = pricingLineItems.ToJsonString();
            var currencyCodeString = checkout.currencyCode().ToString("G");
            CountryCodeString = shopMetadata.PaymentSettings.countryCode().ToString("G");
            var requiresShipping = checkout.requiresShipping();

#if !SHOPIFY_MONO_UNIT_TEST
            object[] args = {
                MerchantName,
                key,
                pricingLineItemsString,
                currencyCodeString,
                CountryCodeString,
                requiresShipping
            };
            AndroidPayCheckoutSession.Call("checkoutWithAndroidPay", args);
            if (AndroidPayEventBridge == null) {
                AndroidPayEventBridge = GlobalGameObject.AddComponent<AndroidPayEventReceiverBridge>();
                AndroidPayEventBridge.Receiver = this;
            }
#endif
        }

        /// <summary>
        /// Creates an <see cref="AndroidPayEventResponse"/> object based on the <see cref="CartState"/>
        /// data.
        /// </summary>
        /// <returns>
        /// The response object.
        /// </returns>
        private AndroidPayEventResponse GetAndroidPayEventResponse() {
            var checkout = CartState.CurrentCheckout;
            var pricingLineItems = GetPricingLineItemsFromCheckout(checkout);
            var currencyCodeString = checkout.currencyCode().ToString("G");
            var requiresShipping = checkout.requiresShipping();
            var shippingMethods = GetShippingMethods();
            return new AndroidPayEventResponse(MerchantName, pricingLineItems, currencyCodeString,
                CountryCodeString, requiresShipping, shippingMethods);
        }

        /// <summary>
        /// Creates a <see cref="PricingLineItems"/> object based on the <see cref="Checkout"/>
        /// data.
        /// </summary>
        /// <param name="checkout">
        /// A <code>Checkout</code> object to build the <code>PricingLineItems</code> on.
        /// </param>
        /// <returns>
        /// The created <code>PricingLineItems</code> object.
        /// </returns>
        private PricingLineItems GetPricingLineItemsFromCheckout(Checkout checkout) {
            var taxPrice = checkout.totalTax();
            var subtotal = checkout.subtotalPrice();
            var totalPrice = checkout.totalPrice();
            var shippingPrice = (decimal?) null;
            if (checkout.requiresShipping()) {
                try {
                    shippingPrice = checkout.shippingLine().price();
                } catch { }
            }
            return new PricingLineItems(taxPrice, subtotal, totalPrice, shippingPrice);
        }

        /// <summary>
        /// Creates a list of <see cref="ShippingMethod"/> objects based on the for the
        /// current shipping address set to the <see cref="Checkout"/> object.
        /// </summary>
        /// <returns>
        /// The created list of <code>ShippingMethods</code>.
        /// </returns>
        private List<ShippingMethod> GetShippingMethods() {
            var checkout = CartState.CurrentCheckout;
            var shippingMethods = new List<ShippingMethod>();

            try {
                var availableShippingRates = checkout.availableShippingRates().shippingRates();

                foreach (var shippingRate in availableShippingRates) {
                    shippingMethods.Add(new ShippingMethod(shippingRate.title(), shippingRate.price().ToString(), shippingRate.handle()));
                }
            } catch (Exception e) {
                throw new Exception("Attempted to gather information on available shipping rates on CurrentCheckout, but CurrentCheckout do not have those properties queried", e);
            }

            return shippingMethods;
        }

        /// <summary>
        /// Converts a list of card brands to a JSON array string.
        /// </summary>
        /// <param name="cardBrands">
        /// A list of <see cref="CardBrand"/> objects to be converted to JSON.
        /// </param>
        /// <returns>
        /// The JSON array string with card brands.
        /// </returns>
        private string SerializedPaymentNetworksFromCardBrands(List<CardBrand> cardBrands) {
            // TODO same as iOS, reuse it.
            var paymentNetworks = PaymentNetwork.NetworksFromCardBrands(cardBrands);
            return Json.Serialize(paymentNetworks);
        }
    }
}
#endif