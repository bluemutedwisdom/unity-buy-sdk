#if UNITY_ANDROID
namespace Shopify.Unity.SDK.Android {
    using System.Collections.Generic;
    using System.Collections;
    using System;

    /// <summary>
    /// Model class that wraps the checkout status to be sent to the Android plugin.
    /// </summary>
    public class AndroidPayCheckoutResponse : Serializable {
        public enum Status {
            Failure,
            Success
        }

        /// <value>JSON name for the "status" attribute.</value>
        private readonly Status Status_;

        public AndroidPayCheckoutResponse(Status status) {
            Status_ = status;
        }

        public override object ToJson() {
            var dict = new Dictionary<string, object>();
            dict["status"] = Status_.ToString();
            return dict;
        }
    }
}
#endif