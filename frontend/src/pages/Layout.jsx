import {   Outlet } from "react-router";
import Nav from "../components/Nav"; 

const Layout = () => {
  return (
    <>
      <Nav />
      <main>
        <Outlet />
      </main>
    </>
  );
};

export default Layout;
