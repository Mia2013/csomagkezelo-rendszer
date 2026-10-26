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

// Minden API-kérés hibáját ugyanúgy alakítjuk át.
instance.interceptors.response.use(
  (response) => response,
  (error) => Promise.reject(apiError(error)),
);

// Régi megoldás:
// export const getData = async (endpoint, query = {}) => {
//   try {
//     const result = await instance.get(`/${endpoint}`, { params: query });
//     return result.data;
//   } catch (error) {
//     throw apiError(error);
//   }
// };
// Új megoldás:
function authHeaders(publicRequest = false) {
  const token = localStorage.getItem("csomagkezelo_token");
  return { Authorization: publicRequest || !token ? null : "Bearer " + token };
}

// Régi megoldás:
// export const getData = async (endpoint, query = {}) =>
//   (await instance.get("/" + endpoint, { params: query })).data;
// Új megoldás:
export const getData = async (endpoint, query = {}, publicRequest = false) =>
  (await instance.get(endpoint, { params: query, headers: authHeaders(publicRequest) })).data;

// Régi megoldás:
// export const postData = async (endpoint, data) => {
//   try {
//     const result = await instance.post(endpoint, data);
//     return result.data;
//   } catch (error) {
//     throw apiError(error);
//   }
// };
// Új megoldás:
// Régi megoldás:
// export const postData = async (endpoint, data) =>
//   (await instance.post(endpoint, data)).data;
// Új megoldás:
export const postData = async (endpoint, data, publicRequest = false) =>
  (await instance.post(endpoint, data, { headers: authHeaders(publicRequest) })).data;

// Régi megoldás:
// export const putData = async (endpoint, data) => {
//   try {
//     const result = await instance.put(endpoint, data);
//     return result.data;
//   } catch (error) {
//     throw apiError(error);
//   }
// };
// Új megoldás:
// Régi megoldás:
// export const putData = async (endpoint, data) =>
//   (await instance.put(endpoint, data)).data;
// Új megoldás:
export const putData = async (endpoint, data) =>
  (await instance.put(endpoint, data, { headers: authHeaders() })).data;

// Régi megoldás:
// export const deleteData = async (endpoint, query) => {
//   try {
//     const result = await instance.delete(`/${endpoint}`, { params: query });
//     return result.data;
//   } catch (error) {
//     throw apiError(error);
//   }
// };
// Új megoldás:
// Régi megoldás:
// export const deleteData = async (endpoint, query) =>
//   (await instance.delete("/" + endpoint, { params: query })).data;
// Új megoldás:
export const deleteData = async (endpoint, query) =>
  (await instance.delete(endpoint, { params: query, headers: authHeaders() })).data;
