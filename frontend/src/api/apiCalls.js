import axios from "axios";

export const controllers = {
  AUTH: "Auth",
  PACKAGE: "Package",
  USER: "User",
};

export const endpoints = {
  registerUser: `${controllers.AUTH}/register`,
  loginUser: `${controllers.AUTH}/login`,
  createPackage: `${controllers.PACKAGE}`,
  trackPackage: `${controllers.PACKAGE}/track`,
  getUserById: `${controllers.USER}`,
  updateUser: `${controllers.USER}/update`,
};

export const instance = axios.create({
  baseURL: import.meta.env.VITE_API_URL || "https://localhost:7259/api",
  headers: { "Content-Type": "application/json" },
});

function apiError(error) {
  const data = error.response?.data;
  const result = new Error(
    typeof data === "string"
      ? data
      : data?.errors
        ? "Ellenőrizd a megadott adatokat!"
        : data?.detail ||
          "A kérés nem sikerült. Ellenőrizd a szerverkapcsolatot!",
  );
  result.status = error.response?.status;
  result.errors = {};
  for (const [key, messages] of Object.entries(data?.errors || {})) {
    const field = key.charAt(0).toLowerCase() + key.slice(1);
    result.errors[field] = messages.join(" ");
  }
  return result;
}

instance.interceptors.response.use(
  (response) => response,
  (error) => Promise.reject(apiError(error)),
);

function authHeaders(publicRequest = false) {
  const token = localStorage.getItem("csomagkezelo_token");
  return { Authorization: publicRequest || !token ? null : "Bearer " + token };
}

export const getData = async (endpoint, query = {}, publicRequest = false) =>
  (await instance.get(endpoint, { params: query, headers: authHeaders(publicRequest) })).data;

export const postData = async (endpoint, data, publicRequest = false) =>
  (await instance.post(endpoint, data, { headers: authHeaders(publicRequest) })).data;

export const putData = async (endpoint, data) =>
  (await instance.put(endpoint, data, { headers: authHeaders() })).data;

export const deleteData = async (endpoint, query) =>
  (await instance.delete(endpoint, { params: query, headers: authHeaders() })).data;
