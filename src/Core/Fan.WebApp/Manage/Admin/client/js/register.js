/**
 * @see Register.cshtml
 */
new Vue({
	el: "#app",
	data: {
		valid: !1,
		fullName: "",
		fullNameRules: [(a) => !!a.trim() || "Fullname is required"],

		userName: "",
		nameRules: [(a) => !!a.trim() || "Email or username is required"],

		password: "",
		passwordRules: [(a) => !!a.trim() || "Password is required"],

		errMsg: "",
	},
	computed: {
		tok: function () {
			return document.querySelector('input[name="__RequestVerificationToken"][type="hidden"]').value;
		},
		payload: function () {
			return {fullName: this.fullName, userName: this.userName, password: this.password};
		},
	},
	methods: {
		login() {
			axios
				.post("/api/auth/register", this.payload, {headers: {"XSRF-TOKEN": this.tok}})
				.then(() => {
					window.location.replace(this.getQueryParam("ReturnUrl") || "/admin");
				})
				.catch((err) => {
					console.log(err);
					this.errMsg = "Register failed, please try again!";
				});
		},
		getQueryParam(a) {
			let b = window.location.href;
			a = a.replace(/[\[\]]/g, "\\$&");
			let c = new RegExp("[?&]" + a + "(=([^&#]*)|&|#|$)"),
				d = c.exec(b);
			return d ? (d[2] ? decodeURIComponent(d[2].replace(/\+/g, " ")) : "") : null;
		},
	},
});
