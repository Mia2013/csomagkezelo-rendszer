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

export const getData = async (endpoint, query = {}) => {
  try {
    const result = await instance.get(`/${endpoint}`, { params: query });
    return result.data;
  } catch (error) {
    const data = error.response?.data;
    const errorMessage =
      typeof data === "string" ? data : data?.message || error.message;
    throw new Error(errorMessage, { cause: error });
  }
};

export const postData = async (endpoint, data) => {
  try {
    const result = await instance.post(endpoint, data);
    return result.data;
  } catch (error) {
      const data = error.response?.data;
    const errorMessage = typeof data === "string" ? data : data?.message || error.message;
    throw new Error(errorMessage, { cause: error });
  }
};

export const putData = async (endpoint, data) => {
  try {
    const result = await instance.put(endpoint, data);
    return result.data;
  } catch (error) {
      const data = error.response?.data;
    const errorMessage = typeof data === "string" ? data : data?.message || error.message;
    throw new Error(errorMessage, { cause: error });
  }
};

export const deleteData = async (endpoint, query) => {
  try {
    const result = await instance.delete(`/${endpoint}`, { params: query });
    return result.data;
  } catch (error) {
      const data = error.response?.data;
    const errorMessage = typeof data === "string" ? data : data?.message || error.message;
    throw new Error(errorMessage, { cause: error });
  }
};
