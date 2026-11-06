import React from 'react';
import ReactDOM from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import './index.css';
import App from './App';
import { GrpcStreamProvider } from './hooks/GrpcStreamContext';
import { WorkflowCatalogProvider } from './hooks/WorkflowCatalogContext';

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <BrowserRouter>
      <WorkflowCatalogProvider>
        <GrpcStreamProvider>
          <App />
        </GrpcStreamProvider>
      </WorkflowCatalogProvider>
    </BrowserRouter>
  </React.StrictMode>,
);
