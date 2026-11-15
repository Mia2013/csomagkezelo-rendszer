import { Box, Container } from "@mui/material";
import LocalShipping from "@mui/icons-material/LocalShipping";
import PageHeader from "../components/PageHeader";
import PackageForm from "../components/PackageForm";

const CreatePackage = () => {
  return (
    <Box>
      <PageHeader icon={<LocalShipping />} title="Csomagfeladás" />
      <Container>
        <PackageForm />
      </Container>
    </Box>
  );
};
export default CreatePackage;
