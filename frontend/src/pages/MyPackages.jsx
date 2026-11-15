import { Box, Container } from "@mui/material";
import Inventory2 from "@mui/icons-material/Inventory2";
import PageHeader from "../components/PageHeader";
import PackageListUser from "../components/PackageListUser";

const MyPackages = () => {
  return (
    <Box>
      <PageHeader icon={<Inventory2 />} title="Csomagjaim" />
      <Container maxWidth="lg" sx={{ py: 4 }}>
        <PackageListUser />
      </Container>
    </Box>
  );
};
export default MyPackages;
