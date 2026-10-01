import axios from "axios";

export const controllers = {
  AUTH: "Auth",
  USER: "User",
};

export const endpoints = {
  registerUser: `${controllers.AUTH}/register`,
  loginUser: `${controllers.AUTH}/login`,
  updateUser: `${controllers.USER}/update`,
};

export const instance = axios.create({
  baseURL: "https://localhost:7259/api",
  headers: {
    "Content-Type": "application/json",
  },
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
