#if UNITY_ANDROID
namespace Shopify.Unity.SDK.Android {
    using System.Collections.Generic;
    using System.Collections;
    using System;
    using Shopify.Unity.MiniJSON;
    using Shopify.Unity.SDK;

#if !SHOPIFY_MONO_UNIT_TEST
    using UnityEngine;
#endif

    public partial class AndroidNativeCheckout : IAndroidPayEventReceiver {
        private delegate void AndroidPayEventHandlerCompletion(AndroidPayCheckoutResponse.Status status);

        /// <summary>
        /// Callback which is invoked from the Android plugin in response to a call to
        /// </summary>
        /// <see cref="CanCheckout(PaymentSettings, CanCheckoutWithNativePayCallback)"/>.
        /// <param name="serializedMessage">
        /// A <code>bool</code> represented as string indicating whether native checkout
        /// is supported on this device or not.
        /// </param>
        public void OnCanCheckoutWithAndroidPayResult(string serializedMessage) {
            var message = NativeMessage.CreateFromJSON(serializedMessage);
            PendingCanCheckoutWithNativePayCallback(bool.Parse(message.Content));
            PendingCanCheckoutWithNativePayCallback = null;
        }

        /// <summary>
        /// Callback which is invoked from the Android plugin when the shipping address
        /// becomes available on the Android Pay side. This method also gets called when the
        /// user explicitly changes their shipping address to a different one.
        /// </summary>
        /// <param name="serializedMessage">
        /// A <see cref="MailinAddressInput"> object represented as a JSON string
        /// containing the shipping address.
        /// </param>
        public void OnUpdateShippingAddress(string serializedMessage) {
            var message = NativeMessage.CreateFromJSON(serializedMessage);
            var contentDictionary = (Dictionary<string, object>) Json.Deserialize(message.Content);
            var mailingAddressInput = new MailingAddressInput(contentDictionary);
            CartState.SetShippingAddress(mailingAddressInput, (ShopifyError error) => {
                if (error == null) {
                    UpdateShippingLineWithDefault(message);
                } else {
                    RespondError(message, error);
                    OnFailure(error);
                }
            });
        }

        /// <summary>
        /// Callback which is invoked when a shipping method is selected for the current
        /// checkout.
        /// </summary>
        /// <param name="serializedMessage">
        /// A <see cref="ShippingMethod"> object represented as a JSON string
        /// containing the selected shipping method.
        /// </param>
        public void OnUpdateShippingLine(string serializedMessage) {
            var message = NativeMessage.CreateFromJSON(serializedMessage);
            var contentDictionary = (Dictionary<string, object>) Json.Deserialize(message.Content);
            var shippingMethod = ShippingMethod.CreateFromJson(contentDictionary);
            UpdateShippingLine(shippingMethod, message);
        }

        /// <summary>
        /// Callback which is invoked when the user confirms the checkout after
        /// reviewing the confirmation screen.
        /// </summary>
        /// <param name="serializedMessage">
        /// A <see cref="ShippingMethod"> object represented as a JSON string
        /// containing the selected shipping method.
        /// </param>
        public void OnConfirmCheckout(string serializedMessage) {
            var checkout = CartState.CurrentCheckout;
            var message = NativeMessage.CreateFromJSON(serializedMessage);
            var amount = checkout.totalPrice();
            var payment = new NativePayment(message.Content);
            var tokenizedPaymentInput = new TokenizedPaymentInput(
                amount: amount,
                billingAddress: payment.BillingAddress,
                idempotencyKey: payment.TransactionIdentifier,
                paymentData: payment.PaymentData,
                identifier: payment.Identifier,
                type: "android_pay"
            );
            CartState.SetEmailAddress(payment.Email, (ShopifyError error) => {
                if (error == null) {
                    PerformCheckout(tokenizedPaymentInput, message);
                } else {
                    RespondError(message, error);
                    OnFailure(error);
                }
            });
        }

        /// <summary>
        /// Updates the cart state by sending the checkout token.
        /// </summary>
        /// <param name="tokenizedPaymentInput">
        /// A <see cref="TokenizedPaymentInput"> containing all information
        /// needed to complete the checkout.
        /// </param>
        /// <param name="message">
        /// A <see cref="NativeMessage"> object to call back to the Android plugin
        /// when the checkout is successful or failed.
        /// </param>
        private void PerformCheckout(TokenizedPaymentInput tokenizedPaymentInput, NativeMessage message) {
            CartState.CheckoutWithTokenizedPayment(tokenizedPaymentInput, (ShopifyError error) => {
                if (error == null) {
                    AndroidPayCheckoutResponse.Status status = AndroidPayCheckoutResponse.Status.Success;
                    message.Respond(new AndroidPayCheckoutResponse(status).ToJsonString());
                    OnSuccess();
                } else {
                    AndroidPayCheckoutResponse.Status status = AndroidPayCheckoutResponse.Status.Failure;
                    message.Respond(new AndroidPayCheckoutResponse(status).ToJsonString());
                    OnFailure(error);
                }
            });
        }

        /// <summary>
        /// Callback which is invoked when an error occurs on the Android plugin side.
        /// </summary>
        /// <param name="serializedMessage">
        /// A <see cref="NativeMessage"> object that contains an error message
        /// as its content.
        /// </param>
        public void OnError(string serializedMessage) {
            var message = NativeMessage.CreateFromJSON(serializedMessage);
            OnFailure(new ShopifyError(
                ShopifyError.ErrorType.NativePaymentProcessingError,
                message.Content
            ));
        }

        /// <summary>
        /// Callback which is invoked when the user cancels the checkout flow
        /// on the Android plugin side.
        /// </summary>
        /// <param name="serializedMessage">
        /// An empty <see cref="NativeMessage"> object.
        /// </param>
        public void OnCancel(string serializedMessage) {
            OnCanceled();
        }

        /// <summary>
        /// Updates the shipping line with the first entry of the list of available ones.
        /// </summary>
        /// <param name="message">
        /// A <see cref="NativeMessage"> object used to respond to the Android plugin side
        /// about the shipping line update.
        /// </param>
        private void UpdateShippingLineWithDefault(NativeMessage message) {
            var shippingMethods = GetShippingMethods();
            if (shippingMethods.Count > 0) {
                // Set the first shipping method as the default
                UpdateShippingLine(shippingMethods[0], message);
            }
        }

        /// <summary>
        /// Updates the shipping line and responds to Android plugin with the update status.
        /// </summary>
        /// <param name="shippingMethod">
        /// A <see cref="ShippingMethod"> object that will be used to update the shipping line.
        /// </param>
        /// <param name="message">
        /// A <see cref="NativeMessage"> object used to respond to the Android plugin side
        /// about the shipping line update.
        /// </param>
        private void UpdateShippingLine(ShippingMethod shippingMethod, NativeMessage message) {
            CartState.SetShippingLine(shippingMethod.Identifier, (ShopifyError error) => {
                if (error == null) {
                    message.Respond(GetAndroidPayEventResponse().ToJsonString());
                } else {
                    RespondError(message, error);
                    OnFailure(error);
                }
            });
        }

        /// <summary>
        /// Responds to the Android plugin side with an error.
        /// </summary>
        /// <param name="message">
        /// A <see cref="NativeMessage"> object used to respond
        /// to the Android plugin side about the error.
        /// </param>
        /// <param name="error">
        /// A <see cref="ShopifyError"> object that will be sent
        /// to the Android plugin side.
        /// </param>
        private void RespondError(NativeMessage message, ShopifyError error) {
            message.Respond(error.ToJsonString());
        }
    }
}
#endif