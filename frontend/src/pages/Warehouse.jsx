import { useEffect, useState } from "react";
import Warehouse from "@mui/icons-material/Warehouse";
import LocalShipping from "@mui/icons-material/LocalShipping";

import { endpoints, getData } from "../api/apiCalls";
import { useAuth } from "../provider/AuthContext";
import Dashboard from "../components/Dashboard";
import WarehouseList from "../components/WarehouseList";

const WarehousePage = () => {
  const { setAlert } = useAuth();
  const [packages, setPackages] = useState([]);
  const [selectedId, setSelectedId] = useState("pickup");

  const loadPackages = () =>
    getData(endpoints.unassignedPrebooked)
      .then(setPackages)
      .catch((error) => setAlert({ message: error.message, severity: "error" }));

  useEffect(() => {
    loadPackages();
  }, [setAlert]);

  const options = [
    {
      id: "pickup",
      title: "Felvételre váró csomagok",
      icon: <LocalShipping />,
      count: packages.length,
      render: () => <WarehouseList packages={packages} onChanged={loadPackages} />,
    },
  ];

  const selected = options.find((option) => option.id === selectedId);

  return (
    <Dashboard
      title="Raktár"
      icon={<Warehouse />}
      cards={options}
      selectedId={selectedId}
      onSelect={setSelectedId}
    >
      {selected.render()}
    </Dashboard>
  );
};

export default WarehousePage;
