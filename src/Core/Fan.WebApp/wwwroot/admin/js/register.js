/**
 * @see Register.cshtml
 */
new Vue({
	el: "#app",
	data: {
		valid: !1,
		name: "",
		nameRules: [(a) => !!a.trim() || "Name is invalid"],

		email: "",
		emailRules: [
			(a) =>
				/^(([^<>()[\]\\.,;:\s@"]+(\.[^<>()[\]\\.,;:\s@"]+)*)|(".+"))@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\])|(([a-zA-Z\-0-9]+\.)+[a-zA-Z]{2,}))$/.test(
					a.trim()
				) || "Email is invalid",
		],

		username: "",
		usernameRules: [(a) => /^[a-zA-Z0-9_-]{1,}$/.test(a.trim()) || "Username must not contain special characters"],

		password: "",
		passwordRules: [(a) => /^[a-zA-Z0-9_-]{4,16}$/.test(a.trim()) || "Password must not be blank and less than 3 characters"],

		errMsg: "",
	},
	computed: {
		tok: function () {
			return document.querySelector('input[name="__RequestVerificationToken"][type="hidden"]').value;
		},
		payload: function () {
			return {fullName: this.name, userName: this.username, email: this.email, password: this.password};
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
					this.errMsg = err.response?.data || "Register failed, please try again!";
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
