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
};

export const instance = axios.create({
  baseURL: import.meta.env.VITE_API_URL || "https://localhost:7259/api",
  headers: { "Content-Type": "application/json" },
});

function requestConfig(anonymous = false) {
  const token = localStorage.getItem("csomagkezelo_token");
  return { headers: { Authorization: !anonymous && token ? `Bearer ${token}` : null } };
}

function apiError(error) {
  const data = error.response?.data;
  const result = new Error(typeof data === "string" ? data :
    data?.errors ? "Ellenőrizd a megadott adatokat!" :
    data?.detail || "A kérés nem sikerült. Ellenőrizd a szerverkapcsolatot!");
  result.status = error.response?.status;
  result.errors = {};
  for (const [key, messages] of Object.entries(data?.errors || {})) {
    const field = key.charAt(0).toLowerCase() + key.slice(1);
    result.errors[field] = messages.join(" ");
  }
  return result;
}

export async function getData(endpoint, query = {}, anonymous = false) {
  try {
    const response = await instance.get(endpoint, { ...requestConfig(anonymous), params: query });
    return response.data;
  } catch (error) { throw apiError(error); }
}

export async function postData(endpoint, data, anonymous = false) {
  try {
    const response = await instance.post(endpoint, data, requestConfig(anonymous));
    return response.data;
  } catch (error) { throw apiError(error); }
}
